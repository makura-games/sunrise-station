using Content.Shared._Sunrise.BloodCult;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.UI;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.BloodCult.TimedProduction;

public sealed partial class CultistFactorySystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultistFactoryComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<CultistFactoryComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CultistFactoryComponent, CultistFactoryItemSelectedMessage>(OnSelected);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var structures = EntityQuery<CultistFactoryComponent>();
        foreach (var structure in structures)
        {
            if (structure.Active)
                continue;

            if (_gameTiming.CurTime > structure.NextTimeUse)
            {
                structure.Active = true;
                UpdateAppearance(structure.Owner, structure);
            }
        }
    }

    private void OnInit(EntityUid uid, CultistFactoryComponent component, ComponentInit args)
    {
        UpdateAppearance(uid, component);
    }

    private void OnInteract(EntityUid uid, CultistFactoryComponent component, InteractHandEvent args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (!HasComp<BloodCultistComponent>(args.User))
            return;

        if (!CanCraft(uid, component, args.User))
            return;

        var xform = Transform(uid);
        if (!xform.Anchored)
            return;

        _ui.SetUiState(uid, CultistAltarUiKey.Key, new CultistFactoryBUIState(component.Products));
        _ui.OpenUi(uid, CultistAltarUiKey.Key, actor.PlayerSession);
    }

    private void OnSelected(EntityUid uid, CultistFactoryComponent component, CultistFactoryItemSelectedMessage args)
    {
        if (!CanCraft(uid, component, args.Actor))
            return;

        foreach (var item in args.Equipment)
        {
            var entity = Spawn(item, Transform(args.Actor).Coordinates);
            _handsSystem.TryPickupAnyHand(args.Actor, entity);
        }

        component.NextTimeUse = _gameTiming.CurTime + TimeSpan.FromSeconds(component.Cooldown);
        component.Active = false;
        UpdateAppearance(uid, component);
    }

    private bool CanCraft(EntityUid uid, CultistFactoryComponent component, EntityUid user)
    {
        if (component.NextTimeUse == null || _gameTiming.CurTime > component.NextTimeUse)
        {
            component.Active = true;
            UpdateAppearance(uid, component);
            return true;
        }

        var name = MetaData(uid).EntityName;
        var totalSeconds = (component.NextTimeUse - _gameTiming.CurTime).Value.TotalSeconds;
        var seconds = Convert.ToInt32(totalSeconds);

        _popupSystem.PopupEntity(Loc.GetString("cultist-factory-charging",
                ("name", name),
                ("seconds", seconds)),
            uid,
            user);

        UpdateAppearance(uid, component);
        return false;
    }

    private void UpdateAppearance(EntityUid uid, CultistFactoryComponent component)
    {
        AppearanceComponent? appearance = null;
        if (!Resolve(uid, ref appearance, false))
            return;

        _appearance.SetData(uid, CultCraftStructureVisuals.Activated, component.Active);
    }
}

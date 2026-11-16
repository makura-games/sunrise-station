using System.Linq;
using System.Threading.Tasks;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Trigger.Systems;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.ExpCollars;

/// <summary>
/// Система для взрывного ошейника.
/// </summary>
public sealed partial class ExpCollarsSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> CannotSuicideTag = "CannotSuicide";

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ExpCollarComponent, AfterInteractEvent>(OnInteraction);
        SubscribeLocalEvent<ExpCollarUserComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ExpCollarComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ExpCollarComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ExpCollarComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ExpCollarComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ExpCollarComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteraction(EntityUid uid, ExpCollarComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !component.IsHost || args.Target == null || !TryComp(args.Target, out ExpCollarComponent? targetComp) || targetComp.IsHost)
            return;

        if (targetComp.Linked.Count != 0 || !targetComp.Virgin)
        {
            _popup.PopupEntity(Loc.GetString("expcollar-connected"), args.Target.Value, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("expcollar-connect"), args.Target.Value, PopupType.MediumCaution);
        component.Linked.Add(args.Target.Value);
        targetComp.Linked.Add(uid);
    }

    private async void OnMobStateChanged(EntityUid uid, ExpCollarUserComponent component, MobStateChangedEvent args)
    {
        if (component.Tool == null || !(args.OldMobState == MobState.Alive && args.NewMobState != MobState.Alive))
            return;

        if (!TryComp<ExpCollarComponent>(component.Tool, out var expCollarComponent))
            return;

        if (expCollarComponent.Armed == false || !expCollarComponent.IsHost)
            return;

        Destroy(component.Tool.Value);
        foreach (var linkedCollar in expCollarComponent.Linked)
        {
            _popup.PopupEntity(Loc.GetString("expcollar-kill"), linkedCollar, PopupType.LargeCaution);
            Destroy(linkedCollar);
        }
    }

    private void OnEquipped(EntityUid uid, ExpCollarComponent component, ClothingGotEquippedEvent args)
    {
        if (TryComp<ExpCollarUserComponent>(args.Wearer, out _))
            return;

        var comp = EnsureComp<ExpCollarUserComponent>(args.Wearer);
        comp.Tool = uid;
        component.Wearer = args.Wearer;

        if (!_mobState.IsAlive(args.Wearer))
            return;

        if (component is { Armed: false, IsHost: false })
        {
            var a = component.Linked.FirstOrDefault();
            if (!TryComp<ExpCollarComponent>(a, out var hostCollarComponent))
                return;
            if (hostCollarComponent.Armed)
            {
                component.Armed = true;
                _popup.PopupEntity(Loc.GetString("expcollar-armed"), uid, PopupType.LargeCaution);
            }
        }

        if (component.IsHost)
        {
            component.Armed = true;
            _tag.AddTag(uid, CannotSuicideTag);
            _popup.PopupEntity(Loc.GetString("expcollar-armed"), args.Wearer, PopupType.LargeCaution);
            foreach (var i in component.Linked)
            {
                if (!TryComp<ExpCollarComponent>(i, out var expCollarComponent))
                    continue;

                if (expCollarComponent.Wearer == null)
                    continue;

                expCollarComponent.Armed = true;
                EnsureComp<SelfUnremovableClothingComponent>(i);
                if (TryComp<ClothingComponent>(i, out var iClothingComponent))
                    _clothing.SetStripDelay((i, iClothingComponent), TimeSpan.FromSeconds(30));
                _popup.PopupEntity(Loc.GetString("expcollar-armed"), i, PopupType.LargeCaution);
            }
        }

        if (component.Armed)
        {
            EnsureComp<SelfUnremovableClothingComponent>(uid);
            if (TryComp<ClothingComponent>(uid, out var clothingComponent))
                _clothing.SetStripDelay((uid, clothingComponent), TimeSpan.FromSeconds(30));
        }
    }

    private void OnUnequipped(EntityUid uid, ExpCollarComponent component, ClothingGotUnequippedEvent args)
    {
        if (!TryComp<ExpCollarUserComponent>(args.Wearer, out var expCollarUserComponent))
            return;

        component.Wearer = null;
        component.Armed = false;
        component.Virgin = false;
        RemComp<ExpCollarUserComponent>(args.Wearer);
        if (HasComp<SelfUnremovableClothingComponent>(uid))
            RemComp<SelfUnremovableClothingComponent>(uid);
        if (TryComp<ClothingComponent>(uid, out var clothingComponent))
            _clothing.SetStripDelay((uid, clothingComponent), TimeSpan.FromSeconds(10));
    }

    private void OnComponentInit(EntityUid uid, ExpCollarComponent component, ComponentInit args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothingComponent))
            return;

        // А вот нехуй в лоадауты такую штуку пихать. Надевать ее надо.
        if (clothingComponent.InSlot != null)
            QueueDel(uid);

        _clothing.SetStripDelay((uid, clothingComponent), component.InitialStripDelay);
    }

    private void OnShutdown(EntityUid uid, ExpCollarComponent component, ComponentShutdown args)
    {
        if (component.Wearer == null)
            return;

        if (!TryComp<ExpCollarUserComponent>(component.Wearer, out var expCollarUserComponent)) // На него насильно надели...
            return;
        RemComp<ExpCollarUserComponent>(component.Wearer.Value);
    }

    private async void Destroy(EntityUid uid)
    {
        if (!TryComp<ExpCollarComponent>(uid, out var collar))
            return;
        if (collar.Wearer == null)
        {
            // Больше нельзя использовать
            RemComp<ExpCollarComponent>(uid);
            return;
        }

        if (collar.Armed == false)
            return;

        _popup.PopupEntity(Loc.GetString("expcollar-boom"), uid, PopupType.LargeCaution);
        _audio.PlayPvs(collar.BeepSound, uid);
        await Task.Delay(TimeSpan.FromSeconds(1));

        for (var i = 10; i > 0; i--)
        {
            _popup.PopupEntity(Loc.GetString("expcollar-popup", ("timer", i)), uid, PopupType.LargeCaution);
            _audio.PlayPvs(collar.BeepSound, uid);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        if (HasComp<ExpCollarUserComponent>(collar.Wearer.Value))
        {
            _damageable.TryChangeDamage(collar.Wearer.Value, collar.Damage);
            _trigger.Trigger(uid);
        }
    }

    private void OnExamined(EntityUid uid, ExpCollarComponent component, ExaminedEvent args)
    {
        args.PushMarkup(component.Armed
            ? Loc.GetString("expcollar-examine-armed")
            : Loc.GetString("expcoller-examine-disarmed"));
        if (component.Wearer == null)
        {
            args.PushMarkup(component.Virgin
                ? Loc.GetString("expcollar-examine-virgin")
                : Loc.GetString("expcollar-examine-unvirgin"));
            if (component.Linked.Count != 0)
            {
                args.PushMarkup(Loc.GetString("expcollar-examine-linked"));
            }
        }
    }
}

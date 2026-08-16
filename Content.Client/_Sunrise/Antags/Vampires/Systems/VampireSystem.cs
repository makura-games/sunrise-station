using Content.Client.Alerts;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Body.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Antags.Vampires.Systems;

public sealed class VampireSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly ProtoId<FactionIconPrototype> MasterIcon = "VampireMasterIcon";
    private const string VampireBloodAlert = "VampireBlood";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VampireComponent, UpdateAlertSpriteEvent>(OnUpdateAlert);
        SubscribeLocalEvent<VampireComponent, GetStatusIconsEvent>(OnVampireIcons);
        SubscribeLocalEvent<VampireComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
    }

    private void OnBeforeInteractHand(Entity<VampireComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (!ent.Comp.FangsExtended || !args.Target.IsValid())
            return;

        if (!HasComp<BloodstreamComponent>(args.Target) &&
            !HasComp<InteractionPopupComponent>(args.Target))
            return;

        args.Handled = true;
    }

    private void OnUpdateAlert(Entity<VampireComponent> ent, ref UpdateAlertSpriteEvent args)
    {
        var comp = ent.Comp;
        var key = args.Alert.AlertKey.AlertType;

        if (key == VampireBloodAlert)
        {
            // Фон задаётся алертом, здесь обновляются только цифры счётчика.
            var value = Math.Clamp(comp.DrunkBlood, 0, 9999);
            var d1 = value / 1000 % 10;
            var d2 = value / 100 % 10;
            var d3 = value / 10 % 10;
            var d4 = value % 10;

            _sprite.LayerSetRsiState((args.SpriteViewEnt, args.SpriteViewEnt.Comp), VampireVisualLayers.Digit1, d1.ToString());
            _sprite.LayerSetRsiState((args.SpriteViewEnt, args.SpriteViewEnt.Comp), VampireVisualLayers.Digit2, d2.ToString());
            _sprite.LayerSetRsiState((args.SpriteViewEnt, args.SpriteViewEnt.Comp), VampireVisualLayers.Digit3, d3.ToString());
            _sprite.LayerSetRsiState((args.SpriteViewEnt, args.SpriteViewEnt.Comp), VampireVisualLayers.Digit4, d4.ToString());
        }
    }

    private void OnVampireIcons(Entity<VampireComponent> ent, ref GetStatusIconsEvent ev)
    {
        if (_prototype.TryIndex(MasterIcon, out var icon))
            ev.StatusIcons.Add(icon);
    }

    private enum VampireVisualLayers : byte
    {
        Digit1,
        Digit2,
        Digit3,
        Digit4,
    }
}

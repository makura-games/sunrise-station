using Content.Client.Alerts;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Systems;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Antags.Vampires.Systems;

public sealed class VampireSystem : SharedVampireSystem
{
    // Отображение крови и статуса вампира.

    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VampireComponent, UpdateAlertSpriteEvent>(OnUpdateAlert);
        SubscribeLocalEvent<VampireComponent, GetStatusIconsEvent>(OnVampireIcons);
    }

    private void OnUpdateAlert(Entity<VampireComponent> ent, ref UpdateAlertSpriteEvent args)
    {
        var component = ent.Comp;
        if (args.Alert.AlertKey.AlertType != component.BloodAlert)
            return;

        // Фон задаётся алертом, здесь обновляются только цифры.
        var value = Math.Clamp(component.DrunkBlood, 0, VampireComponent.MaxDisplayedBlood);
        var digit1 = value / 1000 % 10;
        var digit2 = value / 100 % 10;
        var digit3 = value / 10 % 10;
        var digit4 = value % 10;

        _sprite.LayerSetRsiState(
            (args.SpriteViewEnt, args.SpriteViewEnt.Comp),
            VampireVisualLayers.Digit1,
            digit1.ToString());
        _sprite.LayerSetRsiState(
            (args.SpriteViewEnt, args.SpriteViewEnt.Comp),
            VampireVisualLayers.Digit2,
            digit2.ToString());
        _sprite.LayerSetRsiState(
            (args.SpriteViewEnt, args.SpriteViewEnt.Comp),
            VampireVisualLayers.Digit3,
            digit3.ToString());
        _sprite.LayerSetRsiState(
            (args.SpriteViewEnt, args.SpriteViewEnt.Comp),
            VampireVisualLayers.Digit4,
            digit4.ToString());
    }

    private void OnVampireIcons(Entity<VampireComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var icon))
            args.StatusIcons.Add(icon);
    }
}

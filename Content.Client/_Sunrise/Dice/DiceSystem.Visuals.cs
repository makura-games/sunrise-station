using Content.Shared.Dice;
using Robust.Client.GameObjects;

#pragma warning disable IDE0130 // Пространство имён vanilla-системы сохраняется для partial-расширения.
namespace Content.Client.Dice;

public sealed partial class DiceSystem
{
    private void UpdateSunriseDiceSprite(Entity<DiceComponent> entity, SpriteComponent sprite)
    {
        if (entity.Comp.IsNotStandardDice)
            return;

        // TODO maybe just move each die to its own RSI?
        // If this is ever done keep in mind coin flips also use this system
        var state = _sprite.LayerGetRsiState((entity, sprite), 0).Name;
        if (state == null)
            return;

        var prefix = state[..state.IndexOf('_')];
        _sprite.LayerSetRsiState((entity, sprite), 0, $"{prefix}_{entity.Comp.CurrentValue}");
    }
}

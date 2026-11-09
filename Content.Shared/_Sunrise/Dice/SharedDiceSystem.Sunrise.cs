using Content.Shared._Sunrise.Dice;
using Content.Shared.Examine;
using Robust.Shared.Random;

#pragma warning disable IDE0130 // Пространство имён vanilla-системы сохраняется для partial-расширения.
namespace Content.Shared.Dice;

public abstract partial class SharedDiceSystem
{
    [SubscribeLocalEvent]
    private void OnChangeDiceSetValueMessage(Entity<DiceComponent> entity, ref ChangeDiceSetValueMessage args)
    {
        entity.Comp.SetSides((int) args.StartValue, (int) args.EndValue);

        var message = Loc.GetString("comp-change-dice-sides-amount",
            ("startAmount", (int) args.StartValue),
            ("endAmount", (int) args.EndValue));
        _popup.PopupEntity(message, entity);
        Dirty(entity);
    }

    private bool TryAddSunriseExamine(Entity<DiceComponent> entity, ref ExaminedEvent args)
    {
        if (!entity.Comp.IsNotStandardDice)
            return false;

        args.PushMarkup(Loc.GetString("dice-component-on-examine-message-part-3",
            ("startSide", entity.Comp.StartFromSide),
            ("endSide", entity.Comp.Sides)));
        return true;
    }

    private int GetSunriseRoll(Entity<DiceComponent> entity, IRobustRandom random)
    {
        if (entity.Comp.WeightedValue is { } weightedValue && random.Prob(entity.Comp.WeightedProb))
            return weightedValue;

        return random.Next(entity.Comp.StartFromSide, entity.Comp.Sides + 1);
    }
}

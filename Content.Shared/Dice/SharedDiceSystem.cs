using Content.Shared._Sunrise.Dice;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Dice;

public abstract partial class SharedDiceSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiceComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DiceComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<DiceComponent, ExaminedEvent>(OnExamined);
        // Sunrise-Edit
        SubscribeLocalEvent<DiceComponent, ChangeDiceSetValueMessage>(OnChangeDiceSetValueMessage);
    }

    private void OnUseInHand(Entity<DiceComponent> entity, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        Roll(entity, args.User);
        args.Handled = true;
    }

    private void OnLand(Entity<DiceComponent> entity, ref LandEvent args)
    {
        Roll(entity);
    }

    private void OnExamined(Entity<DiceComponent> entity, ref ExaminedEvent args)
    {
        //No details check, since the sprite updates to show the side.
        using (args.PushGroup(nameof(DiceComponent)))
        {
            // Sunrise-Edit
            if (entity.Comp.IsNotStandardDice)
            {
                args.PushMarkup(Loc.GetString("dice-component-on-examine-message-part-3", ("startSide", entity.Comp.StartFromSide), ("endSide", entity.Comp.Sides)));
            }
            else
            {
                args.PushMarkup(Loc.GetString("dice-component-on-examine-message-part-1", ("sidesAmount", entity.Comp.Sides)));
            }
            // Sunrise-Edit-End
            args.PushMarkup(Loc.GetString("dice-component-on-examine-message-part-2",
                ("currentSide", entity.Comp.CurrentValue)));
        }
    }

    private void SetCurrentSide(Entity<DiceComponent> entity, int side)
    {
        if (side < 1 || side > entity.Comp.Sides)
        {
            Log.Error($"Attempted to set die {ToPrettyString(entity)} to an invalid side ({side}).");
            return;
        }

        entity.Comp.CurrentValue = (side - entity.Comp.Offset) * entity.Comp.Multiplier;
        Dirty(entity);
    }

    public void SetCurrentValue(Entity<DiceComponent> entity, int value)
    {
        if (value % entity.Comp.Multiplier != 0 || value / entity.Comp.Multiplier + entity.Comp.Offset < 1)
        {
            Log.Error($"Attempted to set die {ToPrettyString(entity)} to an invalid value ({value}).");
            return;
        }

        SetCurrentSide(entity, value / entity.Comp.Multiplier + entity.Comp.Offset);
    }

    private void Roll(Entity<DiceComponent> entity, EntityUid? user = null)
    {
        var rand = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(entity));

        // Sunrise-Edit
        var roll = rand.Next(entity.Comp.StartFromSide, entity.Comp.Sides + 1);
        // Sunrise-Edit-End
        SetCurrentSide(entity, roll);

        var popupString = Loc.GetString("dice-component-on-roll-land",
            ("die", entity),
            ("currentSide", entity.Comp.CurrentValue));
        _popup.PopupPredicted(popupString, entity, user);
        _audio.PlayPredicted(entity.Comp.Sound, entity, user);
    }

    // Sunrise-Edit
    private void OnChangeDiceSetValueMessage(Entity<DiceComponent> entity, ref ChangeDiceSetValueMessage args)
    {
        entity.Comp.SetSides((int)args.StartValue, (int)args.EndValue);
        _popup.PopupPredicted(Loc.GetString("comp-change-dice-sides-amount", ("startAmount", (int)args.StartValue), ("endAmount", (int)args.EndValue)), entity, entity.Owner);
        Dirty(entity);
    }
}

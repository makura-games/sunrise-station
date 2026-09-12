using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Body.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

public abstract class SharedVampireSystem : EntitySystem
{
    // Общие проверки и модификаторы вампира.

    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<VampireComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
    }

    private static void OnRefreshMovementSpeed(
        Entity<VampireComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.BloodFullness > 0f)
            return;

        args.ModifySpeed(ent.Comp.StarvationWalkSpeedModifier, ent.Comp.StarvationSprintSpeedModifier);
    }

    private void OnBeforeInteractHand(Entity<VampireComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (args.Handled || !ent.Comp.FangsExtended)
            return;

        var target = args.Target;
        if (!Exists(target) || target == ent.Owner)
            return;

        if (!HasComp<BloodstreamComponent>(target))
        {
            if (HasComp<InteractionPopupComponent>(target))
                args.Handled = true;

            return;
        }

        args.Handled = true;
        TryStartDrinkBlood(ent, target);
    }

    protected virtual bool TryStartDrinkBlood(Entity<VampireComponent> ent, EntityUid target)
    {
        if (!TryComp<BloodSourceComponent>(target, out var bloodSource))
        {
            ShowDrinkPopup(ent, target, "vampire-drink-target-unsuitable");
            return false;
        }

        if (bloodSource.Value <= 0f)
        {
            LocId message = bloodSource.Kind switch
            {
                BloodType.Slime => "vampire-drink-target-slime",
                BloodType.Acid => "vampire-drink-target-acid",
                BloodType.Confectionery => "vampire-drink-target-confectionery",
                _ => "vampire-drink-target-unsuitable",
            };

            ShowDrinkPopup(ent, target, message);
            return false;
        }

        if (!TryComp<BloodstreamComponent>(target, out var bloodstream) ||
            !HasBloodToDrink((target, bloodstream)))
        {
            ShowDrinkPopup(ent, target, "vampire-drink-target-empty");
            return false;
        }

        if (bloodSource.Kind == BloodType.Tainted)
            ShowDrinkPopup(ent, target, "vampire-drink-target-tainted");

        return true;
    }

    private void ShowDrinkPopup(Entity<VampireComponent> ent, EntityUid target, LocId message)
    {
        _popup.PopupPredicted(
            Loc.GetString(message),
            target,
            ent.Owner,
            PopupType.MediumCaution);
    }

    private bool HasBloodToDrink(Entity<BloodstreamComponent?> target)
        => _bloodstream.GetBloodLevel(target) > 0f;
}

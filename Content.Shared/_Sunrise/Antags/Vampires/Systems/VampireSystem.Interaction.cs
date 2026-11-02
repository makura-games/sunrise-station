using Content.Shared.Interaction.Components;
using Content.Shared.Interaction;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Body.Components;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    [SubscribeLocalEvent]
    public static void OnRefreshMovementSpeed(
        Entity<VampireComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.BloodFullness > 0f)
            return;

        args.ModifySpeed(ent.Comp.StarvationWalkSpeedModifier, ent.Comp.StarvationSprintSpeedModifier);
    }

    [SubscribeLocalEvent]
    public void OnBeforeInteractHand(Entity<VampireComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (args.Handled || !ent.Comp.FangsExtended)
            return;

        var target = args.Target;
        if (!Exists(target) || target == ent.Owner)
            return;

        if (!_bloodstreamQuery.HasComp(target))
        {
            if (HasComp<InteractionPopupComponent>(target))
                args.Handled = true;

            return;
        }

        args.Handled = true;
        TryStartDrinkBlood(ent, target);
    }

    private bool CanDrinkBlood(Entity<VampireComponent> ent, EntityUid target)
    {
        if (!_bloodSourceQuery.TryComp(target, out var bloodSource))
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

        if (!_bloodstreamQuery.TryComp(target, out var bloodstream) ||
            _blood.GetBloodLevel((target, bloodstream)) <= 0f)
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
        _popup.PopupEntity(
            Loc.GetString(message),
            target,
            ent.Owner,
            PopupType.MediumCaution);
    }
}

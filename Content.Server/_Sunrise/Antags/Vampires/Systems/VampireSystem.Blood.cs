using Content.Server.Bible.Components;
using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Flash;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Учёт и расход крови.

    private void ProcessBloodDecay(
        Entity<VampireComponent> ent,
        VampireFeedingComponent feeding,
        float elapsed)
    {
        var before = ent.Comp.BloodFullness;
        var wasStarving = before <= 0f;

        if (before > 0f)
        {
            feeding.StarvationDrunkBloodDrainAccumulator = 0f;
            ent.Comp.BloodFullness = MathF.Max(0f, before - feeding.FullnessDecayPerSecond * elapsed);

            if (!MathHelper.CloseTo(ent.Comp.BloodFullness, before))
            {
                DirtyField(ent, ent.Comp, nameof(VampireComponent.BloodFullness));
                UpdateVampireFedAlert(ent);
            }
        }

        var isStarving = ent.Comp.BloodFullness <= 0f;
        if (wasStarving != isStarving)
            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

        if (ent.Comp.BloodFullness > 0f ||
            feeding.StarvationDrunkBloodDrainPerSecond <= 0 ||
            ent.Comp.DrunkBlood <= 0)
        {
            return;
        }

        feeding.StarvationDrunkBloodDrainAccumulator += feeding.StarvationDrunkBloodDrainPerSecond * elapsed;
        var drained = Math.Min(ent.Comp.DrunkBlood, (int)feeding.StarvationDrunkBloodDrainAccumulator);
        if (drained <= 0)
            return;

        feeding.StarvationDrunkBloodDrainAccumulator -= drained;
        TrySpendBlood(ent, drained, showPopup: false);
    }

    internal bool CheckAndConsumeBloodCost(
        Entity<VampireComponent> ent,
        EntityUid? actionEntity = null,
        int bloodCost = 0)
    {
        if (!TryResolveVampireActionCost(ent, actionEntity, bloodCost, out var resolvedCost))
            return false;

        return CanSpendBlood(ent, resolvedCost) && TrySpendBlood(ent, resolvedCost);
    }

    internal bool CanSpendBlood(Entity<VampireComponent> ent, int bloodCost, bool showPopup = true)
    {
        if (bloodCost <= 0 || ent.Comp.DrunkBlood >= bloodCost)
            return true;

        if (showPopup)
            _popup.PopupEntity(Loc.GetString("vampire-not-enough-blood"), ent.Owner, ent.Owner);

        return false;
    }

    internal bool TrySpendBlood(Entity<VampireComponent> ent, int bloodCost, bool showPopup = true)
    {
        if (!CanSpendBlood(ent, bloodCost, showPopup))
            return false;

        if (bloodCost <= 0)
            return true;

        ent.Comp.DrunkBlood -= bloodCost;
        DirtyField(ent, ent.Comp, nameof(VampireComponent.DrunkBlood));
        UpdateVampireAlert(ent);
        return true;
    }

    internal int AddBlood(
        Entity<VampireComponent> ent,
        float amount,
        EntityUid? target = null,
        bool countTotalBlood = true,
        bool recordTarget = true)
    {
        if (amount <= 0f || !TryComp<VampireFeedingComponent>(ent, out var feeding))
            return 0;

        var storedAmount = amount + feeding.DrunkBloodRemainder;
        var integerAmount = Math.Max(0, (int)storedAmount);
        feeding.DrunkBloodRemainder = storedAmount - integerAmount;
        var wasStarving = ent.Comp.BloodFullness <= 0f;

        if (integerAmount > 0)
        {
            ent.Comp.DrunkBlood += integerAmount;
            DirtyField(ent, ent.Comp, nameof(VampireComponent.DrunkBlood));
        }

        var totalBloodAdded = 0;
        if (countTotalBlood)
        {
            var totalAmount = amount + feeding.TotalBloodRemainder;
            totalBloodAdded = Math.Max(0, (int)totalAmount);
            feeding.TotalBloodRemainder = totalAmount - totalBloodAdded;
            feeding.TotalBlood += totalBloodAdded;
        }

        if (recordTarget && target is { } targetUid)
        {
            var isNewTarget = !feeding.BloodDrunkFromTargets.TryGetValue(targetUid, out var targetBlood);
            feeding.BloodDrunkFromTargets[targetUid] = targetBlood + amount;

            if (isNewTarget && countTotalBlood)
                feeding.UniqueVictims++;
        }

        ent.Comp.BloodFullness = MathF.Min(feeding.MaxBloodFullness, ent.Comp.BloodFullness + amount);
        DirtyField(ent, ent.Comp, nameof(VampireComponent.BloodFullness));

        var isStarving = ent.Comp.BloodFullness <= 0f;
        if (wasStarving != isStarving)
            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

        UpdateVampireAlert(ent);
        UpdateVampireFedAlert(ent);

        if (totalBloodAdded > 0)
            UpdatePowerLevel(ent);

        return integerAmount;
    }

    private bool TryResolveVampireActionCost(
        Entity<VampireComponent> ent,
        EntityUid? actionEntity,
        int bloodCost,
        out int resolvedCost,
        bool showPopup = true)
    {
        resolvedCost = Math.Max(0, bloodCost);

        if (actionEntity is not { } action)
            return true;

        if (!Exists(action))
            return false;

        if (!TryComp<VampireActionComponent>(action, out var vampireAction))
            return true;

        if (ent.Comp.PowerLevel < vampireAction.RequiredPowerLevel)
        {
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("action-vampire-not-enough-power"), ent.Owner, ent.Owner);

            return false;
        }

        if (resolvedCost <= 0 && vampireAction.BloodCost > 0)
            resolvedCost = vampireAction.BloodCost;

        return true;
    }

    // Возможно, можно добавить сюда расу милир

    internal bool IsProtectedByFaith(EntityUid target)
        => HasComp<BibleUserComponent>(target);

    private bool HasFlashProtection(EntityUid target)
    {
        var attempt = new FlashAttemptEvent(target, null, null);
        RaiseLocalEvent(target, ref attempt, true);
        return attempt.Cancelled;
    }
}

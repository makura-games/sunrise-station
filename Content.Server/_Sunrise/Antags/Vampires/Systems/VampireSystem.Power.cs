using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Damage;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Прогрессия силы вампира.

    private void UpdatePowerLevel(Entity<VampireComponent> ent, bool syncActions = true)
    {
        if (!TryComp<VampireConfigurationComponent>(ent, out var configuration) ||
            !TryComp<VampireFeedingComponent>(ent, out var feeding))
        {
            return;
        }

        var oldLevel = ent.Comp.PowerLevel;
        var newLevel = oldLevel;

        foreach (var prototype in _prototype.EnumeratePrototypes<VampirePowerLevelPrototype>())
        {
            if (prototype.Level > configuration.MaxProgressionLevel ||
                prototype.Level <= newLevel ||
                prototype.RequiredTotalBlood is not { } requiredTotalBlood ||
                feeding.TotalBlood < requiredTotalBlood)
            {
                continue;
            }

            newLevel = prototype.Level;
        }

        if (newLevel == oldLevel)
            return;

        ent.Comp.PowerLevel = newLevel;
        ApplyPowerLevelSettings(ent);
        DirtyField(ent, ent.Comp, nameof(VampireComponent.PowerLevel));

        if (syncActions)
            SyncVampireActions(ent);

        if (!configuration.PowerLevelMessages.TryGetValue(newLevel, out var levelUpMessage))
            return;

        _antag.SendBriefing(ent, Loc.GetString(levelUpMessage), Color.Crimson, null);
    }

    private void ApplyPowerLevelSettings(Entity<VampireComponent> ent)
    {
        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level) ||
            !TryComp<VampireFeedingComponent>(ent, out var feeding))
        {
            return;
        }

        var fangs = level.Fangs;

        feeding.MaxBloodFullness = level.MaxBloodFullness;
        feeding.FullnessDecayPerSecond = level.FullnessDecayPerSecond;
        ent.Comp.BloodFullness = MathF.Min(ent.Comp.BloodFullness, feeding.MaxBloodFullness);

        feeding.SipInterval = fangs.SipInterval;
        feeding.BloodGainPerSip = fangs.BloodGain;
        feeding.TargetBloodDrainPerSip = fangs.TargetBloodDrain;
        feeding.BiteDamage = new DamageSpecifier(fangs.BiteDamage);
        feeding.BiteBleedAmount = fangs.BleedAmount;
        feeding.BiteDistanceThreshold = fangs.Range;
        feeding.MaxBloodPerTarget = fangs.MaxBloodPerTarget;
        feeding.Healing = new DamageSpecifier(fangs.Healing);

        DirtyField(ent, ent.Comp, nameof(VampireComponent.BloodFullness));
        UpdateVampireFedAlert(ent);
    }

    private bool TryGetPowerLevelPrototype(
        VampirePowerLevel powerLevel,
        out VampirePowerLevelPrototype prototype)
    {
        foreach (var candidate in _prototype.EnumeratePrototypes<VampirePowerLevelPrototype>())
        {
            if (candidate.Level != powerLevel)
                continue;

            prototype = candidate;
            return true;
        }

        _sawmill.Error($"Missing vampire power level prototype for {powerLevel}");
        prototype = default!;
        return false;
    }
}

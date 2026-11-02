using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    private void UpdatePowerLevel(Entity<VampireComponent> ent, bool syncActions = true)
    {
        if (!_configurationQuery.TryComp(ent, out var configuration) ||
            !_feedingQuery.TryComp(ent, out var feeding))
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

        var ev = new VampirePowerLevelChangedEvent(newLevel);
        RaiseLocalEvent(ent, ref ev);
    }

    private void ApplyPowerLevelSettings(Entity<VampireComponent> ent)
    {
        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return;

        ent.Comp.BloodFullness = MathF.Min(ent.Comp.BloodFullness, level.MaxBloodFullness);
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

        Log.Error($"Не найден прототип уровня силы вампира {powerLevel}");
        prototype = default!;
        return false;
    }
}

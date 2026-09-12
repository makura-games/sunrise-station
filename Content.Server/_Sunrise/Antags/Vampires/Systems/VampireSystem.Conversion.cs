using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Server.Roles;
using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Обращение в вампира и выдача целей.

    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly NumberObjectiveSystem _number = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly RoleSystem _role = default!;


    private void OnBloodDrainGetProgress(Entity<BloodDrainConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var target = _number.GetTarget(ent);
        if (args.Mind.OwnedEntity is not { } body ||
            !TryComp<VampireFeedingComponent>(body, out var feeding))
        {
            args.Progress = 0f;
            return;
        }

        args.Progress = target > 0f
            ? MathF.Min((float)feeding.TotalBlood / target, 1f)
            : 1f;
    }

    /// <summary>
    /// Проверяет возможность обращения.
    /// </summary>
    public bool CanMakeVampire(EntityUid target)
    {
        return target.IsValid() &&
            Exists(target) &&
            !HasComp<VampireComponent>(target) &&
            _mind.TryGetMind(target, out _, out _);
    }

    /// <summary>
    /// Обращает сущность в вампира.
    /// </summary>
    public bool TryMakeVampire(EntityUid target)
    {
        if (!CanMakeVampire(target))
            return false;

        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return false;

        var configuration = EnsureComp<VampireConfigurationComponent>(target);
        EnsureComp<VampireComponent>(target);
        _role.MindAddRole(mindId, configuration.MindRole, mind, silent: true);

        foreach (var objective in configuration.Objectives)
        {
            TryAddVampireObjective((mindId, mind), objective);
        }

        foreach (var objectiveGroup in configuration.ObjectiveGroups)
        {
            TryAddRandomVampireObjective(
                (mindId, mind),
                objectiveGroup,
                configuration.ObjectiveMaxDifficulty);
        }

        var briefing = Loc.GetString("vampire-role-greeting");
        _antag.SendBriefing(target, briefing, Color.Yellow, configuration.BriefingSound);
        return true;
    }

    private void TryAddVampireObjective(Entity<MindComponent> mind, EntProtoId objective)
    {
        if (_mind.TryAddObjective(mind, mind.Comp, objective))
            return;

        _sawmill.Error(
            $"Failed to add vampire objective {objective} to {ToPrettyString(mind.Comp.OwnedEntity)}");
    }

    private void TryAddRandomVampireObjective(
        Entity<MindComponent> mind,
        ProtoId<WeightedRandomPrototype> objectiveGroup,
        float maxDifficulty)
    {
        if (_objectives.GetRandomObjective(
                mind,
                mind.Comp,
                objectiveGroup,
                maxDifficulty) is { } objective)
        {
            _mind.AddObjective(mind, mind.Comp, objective);
            return;
        }

        _sawmill.Error(
            $"Failed to select vampire objective from group {objectiveGroup} " +
            $"for {ToPrettyString(mind.Comp.OwnedEntity)}");
    }
}

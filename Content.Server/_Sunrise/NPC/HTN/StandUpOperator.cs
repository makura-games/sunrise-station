using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.HTN;
using Content.Server.NPC;
using Content.Shared.Standing;
using Content.Shared.DoAfter;
using System.Linq;
using Content.Shared.Stunnable;

namespace Content.Server._Sunrise.NPC.HTN;

/// <summary>
/// Пытаемся заставить NPC встать, если он еще не стоит.
/// </summary>
public sealed partial class StandUpOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private StandingStateSystem _standing = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    private EntityQuery<StandingStateComponent> _standingQuery;
    private EntityQuery<DoAfterComponent> _doAfterQuery;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _standing = sysManager.GetEntitySystem<StandingStateSystem>();
        _standingQuery = _entManager.GetEntityQuery<StandingStateComponent>();
        _doAfterQuery = _entManager.GetEntityQuery<DoAfterComponent>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_standingQuery.TryGetComponent(owner, out var standing) ||
            standing.Standing)
            return;

        if (_doAfterQuery.TryGetComponent(owner, out var doAfter) &&
            doAfter.DoAfters.Values.Any(x => x.Args.Event is TryStandDoAfterEvent && !x.Cancelled && !x.Completed))
            return;

        _entManager.Dirty(owner, standing);
        _standing.Down(owner);
        _standing.Stand(owner, standing);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_standingQuery.TryGetComponent(owner, out var standing) ||
            standing.Standing)
            return HTNOperatorStatus.Finished;

        if (_doAfterQuery.TryGetComponent(owner, out var doAfter) &&
            doAfter.DoAfters.Values.Any(x => x.Args.Event is TryStandDoAfterEvent && !x.Cancelled && !x.Completed))
            return HTNOperatorStatus.Continuing;

        _entManager.Dirty(owner, standing);
        _standing.Down(owner);
        _standing.Stand(owner, standing);

        return HTNOperatorStatus.Continuing;
    }
}

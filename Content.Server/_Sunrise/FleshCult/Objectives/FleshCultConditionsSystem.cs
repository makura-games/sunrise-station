using Content.Server._Sunrise.FleshCult.GameRule;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Shared._Sunrise.FleshCult;
using Content.Shared.GameTicking.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server._Sunrise.FleshCult.Objectives;

public sealed partial class FleshCultConditionsSystem : EntitySystem
{
    [Dependency] private NumberObjectiveSystem _number = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CreateFleshHeartConditionComponent, ObjectiveGetProgressEvent>(OnFleshHeartGetProgress);
    }

    private void OnFleshHeartGetProgress(EntityUid uid, CreateFleshHeartConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = FleshHeartProgress(args.MindId, _number.GetTarget(uid));
    }

    private float FleshHeartProgress(EntityUid? mindId, int target)
    {
        // prevent divide-by-zero
        if (target == 0)
            return 1f;

        if (!TryComp<FleshCultistRoleComponent>(mindId, out var role))
            return 0f;

        var query = EntityQueryEnumerator<FleshCultRuleComponent, GameRuleComponent>();

        while (query.MoveNext(out var uid, out var fleshCult, out var gameRule))
        {
            return CalculateFleshHeartStatus(fleshCult.FleshHearts);
        }

        return 0f;
    }

    private float CalculateFleshHeartStatus(Dictionary<EntityUid, FleshHeartStatus> fleshHearts)
    {
        var hasBaseHeart = false;
        foreach (var fleshHeart in fleshHearts)
        {
            switch (fleshHeart.Value)
            {
                case FleshHeartStatus.Active:
                case FleshHeartStatus.Final:
                    return 1f;

                case FleshHeartStatus.Base:
                    hasBaseHeart = true;
                    break;

                case FleshHeartStatus.Destruction:
                    break;
            }
        }

        return hasBaseHeart ? 0.5f : 0f;
    }
}

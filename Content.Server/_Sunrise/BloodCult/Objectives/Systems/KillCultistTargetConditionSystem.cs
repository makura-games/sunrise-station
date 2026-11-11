using System.Diagnostics;
using System.Linq;
using Content.Server._Sunrise.BloodCult.GameRule;
using Content.Server._Sunrise.BloodCult.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles.Jobs;

namespace Content.Server._Sunrise.BloodCult.Objectives.Systems;

public sealed partial class KillCultistTargetsConditionSystem : EntitySystem
{
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KillCultistTargetsConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);

        SubscribeLocalEvent<KillCultistTargetsConditionComponent, ObjectiveAssignedEvent>(OnPersonAssigned);
    }

    public void RefresTitle(EntityUid uid, Dictionary<EntityUid, bool> targets, KillCultistTargetsConditionComponent comp)
    {
        _metaData.SetEntityName(uid, GetTitle(targets, comp.Title));
    }

    private string GetTitle(Dictionary<EntityUid, bool> targets, string title)
    {
        var targetsList = "";
        foreach (var target in targets)
        {
            if (!_mind.TryGetMind(target.Key, out var mindId, out var mind) || mind.CharacterName == null)
                continue;

            var targetName = mind.CharacterName;
            var jobName = _job.MindTryGetJobName(mindId);
            var sacrificed = target.Value ? "\u2714" : "\u2718";
            targetsList += Loc.GetString("objective-condition-cult-kill-target",
                ("targetName", targetName),
                ("job", jobName),
                ("status", sacrificed));
            targetsList += "\n";
        }

        return Loc.GetString(title, ("targets", targetsList));
    }

    private void OnGetProgress(EntityUid uid,
        KillCultistTargetsConditionComponent comp,
        ref ObjectiveGetProgressEvent args)
    {
        args.Progress = KillCultistTargetsProgress();
    }

    private void OnPersonAssigned(EntityUid uid,
        KillCultistTargetsConditionComponent component,
        ref ObjectiveAssignedEvent args)
    {
        var cultistRule = EntityQuery<BloodCultRuleComponent>().FirstOrDefault();

        if (cultistRule == null)
            return;

        _metaData.SetEntityName(uid, GetTitle(cultistRule.CultTargets, component.Title));
    }

    private float KillCultistTargetsProgress()
    {
        var cultistRule = EntityQuery<BloodCultRuleComponent>().FirstOrDefault();
        Debug.Assert(cultistRule != null, nameof(cultistRule) + " != null");
        var cultTargets = cultistRule.CultTargets;

        var targetsCount = cultTargets.Count;

        // prevent divide-by-zero
        if (targetsCount == 0)
            return 1f;

        var deadTargetsCount = 0;

        foreach (var cultTarget in cultTargets)
        {
            if (cultTarget.Value)
            {
                deadTargetsCount += 1;
            }
        }

        return deadTargetsCount / (float)targetsCount;
    }
}

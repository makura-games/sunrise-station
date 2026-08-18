using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives.Systems;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Ninja.Components;
using Content.Shared.Shuttles.Components;
using Robust.Server.Player;
using Robust.Shared.Random;
using Robust.Server.GameObjects;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server._Sunrise.GameTicking.Rules.Components;
using System.Diagnostics.CodeAnalysis;
using Content.Server._Sunrise.Shuttles.Components;
using Content.Server._Sunrise.Helpers;

namespace Content.Server._Sunrise.GameTicking.Rules;

public sealed class NinjaRuleSystem : GameRuleSystem<NinjaRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly CodeConditionSystem _codeCondition = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SunriseHelpersSystem _helpers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
    }

    protected override void Started(
        EntityUid uid,
        NinjaRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        if (!_helpers.TryGetRandomStation(out var eligible))
            return;

        component.TargetStation = eligible;
    }

    protected override void Ended(
        EntityUid uid,
        NinjaRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleEndedEvent args)
    {
        RemoveOutpostFtl(component);
    }

    protected override void AppendRoundEndText(
        EntityUid uid,
        NinjaRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        args.AddLine(Loc.GetString("ninja-round-end-title"));

        foreach (var (_, sessionData, name) in _antag.GetAntagIdentifiers(uid))
        {
            args.AddLine(Loc.GetString("ninja-round-end-name-user",
                ("name", name),
                ("user", sessionData.UserName)));
        }

        args.AddLine(Loc.GetString(component.EscapedOnShuttle
            ? "ninja-round-end-escaped"
            : "ninja-round-end-not-escaped"));
    }

    protected override void ActiveTick(
        EntityUid uid,
        NinjaRuleComponent component,
        GameRuleComponent gameRule,
        float frameTime)
    {
        if (component.FtlUnlocked)
            return;

        if (Timing.CurTime < component.UpdateNextTime)
            return;

        component.UpdateNextTime = Timing.CurTime + component.ObjectiveCheckInterval;

        // Find this rule's living ninja and check whether all non-return objectives are done.
        if (!TryGetLivingNinjaForRule(uid, null, out _, out var mindId, out var mind))
            return;

        if (!AllNonReturnObjectivesComplete(mindId.Value, mind, component))
            return;

        component.FtlUnlocked = true;
        SetOutpostFtl(component);
        NotifyNinja(mind, Loc.GetString("ninja-ftl-unlocked"));
    }

    private bool AllNonReturnObjectivesComplete(EntityUid mindId, MindComponent mind, NinjaRuleComponent rule)
    {
        foreach (var objUid in mind.Objectives)
        {
            var objProto = Prototype(objUid);

            if (objProto == null)
                continue;

            if (objProto == rule.ReturnObjectiveProto)
                continue;

            if (!_objectives.IsCompleted(objUid, (mindId, mind)))
                return false;
        }

        return true;
    }

    private void SetOutpostFtl(NinjaRuleComponent component)
    {
        if (component.OutpostMapEntity is not { } mapEnt || !Exists(mapEnt))
            return;

        var ftlComp = EnsureComp<FTLDestinationComponent>(mapEnt);

        ftlComp.Enabled = true;
        ftlComp.Whitelist = component.FtlMapWhitelist;

        Dirty(mapEnt, ftlComp);
        _console.RefreshShuttleConsoles();
    }

    private void RemoveOutpostFtl(NinjaRuleComponent component)
    {
        if (component.OutpostMapEntity is not { } mapEnt || !Exists(mapEnt))
            return;

        var ftlComp = EnsureComp<FTLDestinationComponent>(mapEnt);

        ftlComp.Enabled = false;
        ftlComp.Whitelist = null;

        Dirty(mapEnt, ftlComp);
        _console.RefreshShuttleConsoles();
    }

    private void NotifyNinja(MindComponent mind, string message)
    {
        if (mind.UserId is not { } userId)
            return;

        if (!_player.TryGetSessionById(userId, out var session))
            return;

        _chat.DispatchServerMessage(session, message);
    }

    private void OnRuleLoadedGrids(Entity<NinjaRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        var query = EntityQueryEnumerator<NinjaShuttleComponent>();
        while (query.MoveNext(out var uid, out var shuttle))
        {
            if (Transform(uid).MapID != args.Map)
                continue;

            ent.Comp.OutpostMapEntity = _map.GetMap(args.Map);

            shuttle.AssociatedRule = ent;
            break;
        }
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        if (!TryComp<NinjaShuttleComponent>(ev.Entity, out var shuttle))
            return;

        if (shuttle.AssociatedRule is not { } ruleUid)
            return;

        if (!TryComp<NinjaRuleComponent>(ruleUid, out var rule))
            return;

        // Find this rule's living ninja aboard the departing shuttle.
        if (!TryGetLivingNinjaForRule(ruleUid, ev.Entity, out _, out var mindId, out var mind))
            return;

        if (!AllNonReturnObjectivesComplete(mindId.Value, mind, rule))
            return;

        rule.EscapedOnShuttle = true;

        if (!_mind.TryFindObjective((mindId.Value, mind), rule.ReturnObjectiveProto, out var objectiveUid))
            return;

        // Complete escape goal
        _codeCondition.SetCompleted(objectiveUid.Value);
    }

    private bool TryGetLivingNinjaForRule(
        EntityUid ruleUid,
        EntityUid? requiredGridUid,
        [NotNullWhen(true)] out EntityUid? ninjaUid,
        [NotNullWhen(true)] out EntityUid? mindId,
        [NotNullWhen(true)] out MindComponent? mind)
    {
        ninjaUid = null;
        mindId = null;
        mind = null;

        if (!TryComp<AntagSelectionComponent>(ruleUid, out var antag))
            return false;

        foreach (var (candidateMindId, _) in antag.AssignedMinds)
        {
            if (!TryComp<MindComponent>(candidateMindId, out var candidateMind))
                continue;

            var candidateNinjaUid = candidateMind.CurrentEntity;

            if (!HasComp<SpaceNinjaComponent>(candidateNinjaUid))
                continue;

            if (!TryComp<MobStateComponent>(candidateNinjaUid, out var mobState) ||
                mobState.CurrentState != MobState.Alive)
                continue;

            if (requiredGridUid != null)
            {
                var xform = Transform(candidateNinjaUid.Value);
                if (xform.GridUid != requiredGridUid)
                    continue;
            }

            ninjaUid = candidateNinjaUid.Value;
            mindId = candidateMindId;
            mind = candidateMind;
            return true;
        }

        return false;
    }
}

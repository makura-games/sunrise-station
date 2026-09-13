using System.Linq;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.RoundEnd;
using Content.Server.Station.Components;
using Content.Shared._Sunrise.FleshCult;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.FleshCult.GameRule;

public sealed partial class FleshCultRuleSystem : GameRuleSystem<FleshCultRuleComponent>
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private RoundEndSystem _roundEndSystem = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    [ValidatePrototypeId<AntagPrototype>]
    private const string LeaderAntagProto = "FleshCultistLeader";
    private static readonly EntProtoId FleshCultGameRule = "FleshCult";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshCultRuleComponent, AfterAntagEntitySelectedEvent>(AfterEntitySelected);
        SubscribeLocalEvent<FleshCultRuleComponent, ObjectivesTextPrependEvent>(OnObjectivesTextPrepend);
        SubscribeLocalEvent<FleshCultSystem.FleshHeartStatusChangeEvent>(OnFleshHeartStatusChange);
    }

    protected override void Started(EntityUid uid,
        FleshCultRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        var eligible = new List<Entity<StationEventEligibleComponent, NpcFactionMemberComponent>>();
        var eligibleQuery = EntityQueryEnumerator<StationEventEligibleComponent, NpcFactionMemberComponent>();
        while (eligibleQuery.MoveNext(out var eligibleUid, out var eligibleComp, out var member))
        {
            if (!_npcFaction.IsFactionHostile(component.Faction, (eligibleUid, member)))
                continue;

            eligible.Add((eligibleUid, eligibleComp, member));
        }

        if (eligible.Count == 0)
            return;

        component.TargetStation = RobustRandom.Pick(eligible);
    }

    protected override void Ended(EntityUid uid, FleshCultRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        foreach (var componentFleshHeart in component.FleshHearts)
        {
            QueueDel(componentFleshHeart.Key);
        }

        foreach (var cultist in component.Cultists)
        {
            RemComp<FleshCultistComponent>(cultist);
        }
    }

    private void OnObjectivesTextPrepend(EntityUid uid, FleshCultRuleComponent comp, ref ObjectivesTextPrependEvent args)
    {
        if (!TryComp(comp.CultistsLeaderMind, out MindComponent? mind))
            return;
        _player.TryGetSessionById(mind.UserId, out var session);
        args.Text += "\n" + Loc.GetString("flesh-cult-round-end-leader", ("name", mind.CharacterName)!, ("username", session!.Name));
    }

    private void OnFleshHeartStatusChange(FleshCultSystem.FleshHeartStatusChangeEvent ev)
    {
        var query = EntityQueryEnumerator<FleshCultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var fleshCult, out var gameRule))
        {
            switch (ev.Status)
            {
                case FleshHeartStatus.Base:
                {
                    fleshCult.FleshHearts.Add(ev.FleshHeartUid, FleshHeartStatus.Base);
                    break;
                }
                case FleshHeartStatus.Active:
                {
                    if (fleshCult.FleshHearts.ContainsKey(ev.FleshHeartUid))
                        fleshCult.FleshHearts[ev.FleshHeartUid] = FleshHeartStatus.Active;
                    break;
                }
                case FleshHeartStatus.Destruction:
                {
                    if (fleshCult.FleshHearts.ContainsKey(ev.FleshHeartUid))
                        fleshCult.FleshHearts[ev.FleshHeartUid] = FleshHeartStatus.Destruction;
                    break;
                }
                case FleshHeartStatus.Final:
                {
                    if (fleshCult.FleshHearts.ContainsKey(ev.FleshHeartUid))
                        fleshCult.FleshHearts[ev.FleshHeartUid] = FleshHeartStatus.Final;
                    _roundEndSystem.ForceSetCountdown(TimeSpan.FromSeconds(10), cantRecall: true);
                    break;
                }
            }
        }
    }

    private void AfterEntitySelected(Entity<FleshCultRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        MakeCultist(args.EntityUid, ent.Comp);
        if (args.Def.PrefRoles.Contains(LeaderAntagProto))
        {
            if (_mindSystem.TryGetMind(args.EntityUid, out var mindId, out var mind))
                ent.Comp.CultistsLeaderMind = mindId;
        }
    }

    public FleshCultRuleComponent StartGameRule()
    {
        var comp = EntityQuery<FleshCultRuleComponent>().FirstOrDefault();
        if (comp == null)
        {
            GameTicker.StartGameRule(FleshCultGameRule, out var ruleEntity);
            comp = Comp<FleshCultRuleComponent>(ruleEntity);
        }

        return comp;
    }

    private bool MakeCultist(EntityUid fleshCultist, FleshCultRuleComponent fleshCultRule)
    {
        if (!_mindSystem.TryGetMind(fleshCultist, out var mindId, out var mind))
            return false;

        if (mind.OwnedEntity is not { } entity)
        {
            Logger.ErrorS("preset", "Mind picked for cultist did not have an attached entity.");
            return false;
        }

        SendCultistBriefing(mindId, fleshCultRule.CultistsNames);

        if (_player.TryGetSessionById(mind.UserId, out var session))
        {
            _audioSystem.PlayGlobal(fleshCultRule.AddedSound, session);
        }

        fleshCultRule.Cultists.Add(fleshCultist);

        return true;
    }

    private void SendCultistBriefing(EntityUid mindId, List<string> cultistsNames)
    {
        if (!_mindSystem.TryGetMind(mindId, out _, out var mind) || !_player.TryGetSessionById(mind.UserId, out var session))
            return;
        _chatManager.DispatchServerMessage(session, Loc.GetString("flesh-cult-role-greeting"));
        _chatManager.DispatchServerMessage(session, Loc.GetString("flesh-cult-role-cult-members", ("cultMembers", string.Join(", ", cultistsNames))));
    }

    protected override void AppendRoundEndText(EntityUid uid,
        FleshCultRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        var result = Loc.GetString("flesh-cult-round-end-count-create-flesh-hearts", ("heartsCount",
            component.FleshHearts.Count));

        var destroyHearts = 0;
        var activateHearts = 0;

        foreach (var (heartUid, heartStatus) in component.FleshHearts)
        {
            switch (heartStatus)
            {
                case FleshHeartStatus.Destruction:
                    destroyHearts++;
                    break;
                case FleshHeartStatus.Active:
                    activateHearts++;
                    break;
                case FleshHeartStatus.Final:
                    activateHearts++;
                    break;
            }
        }

        if (component.FleshHearts.Count > 0)
        {
            if (activateHearts > 0)
            {
                result +=  "\n" + Loc.GetString("flesh-cult-round-end-count-activate-flesh-hearts", ("heartsCount",
                    activateHearts));
            }
            else
            {
                result += "\n" + Loc.GetString("flesh-cult-round-end-count-no-activate-flesh-hearts");
            }

            if (destroyHearts > 0)
            {
                result +=  "\n" + Loc.GetString("flesh-cult-round-end-count-destroy-flesh-hearts", ("heartsCount",
                    destroyHearts));
            }
            else
            {
                result += "\n" + Loc.GetString("flesh-cult-round-end-count-no-destroy-flesh-hearts");
            }
        }

        var fleshHeartActive = false;
        foreach (var fleshCultFleshHeart in component.FleshHearts)
        {
            if (fleshCultFleshHeart.Value is FleshHeartStatus.Active or FleshHeartStatus.Final)
                fleshHeartActive = true;
        }

        if (fleshHeartActive)
        {
            result += "\n" + Loc.GetString("flesh-cult-round-end-flesh-heart-succes");
        }
        else
        {
            result += "\n" + Loc.GetString("flesh-cult-round-end-flesh-heart-fail");
        }

        args.AddLine("\n" + result);
    }
}

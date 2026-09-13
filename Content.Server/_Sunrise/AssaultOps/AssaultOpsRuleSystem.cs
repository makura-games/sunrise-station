using Content.Server._Sunrise.AssaultOps.Icarus;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Revolutionary.Components;
using Content.Server.Explosion.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Server.Station.Components;
using Content.Server.Store.Systems;
using Content.Server.Traitor.Uplink;
using Content.Shared._Sunrise.AssaultOps;
using Content.Shared._Sunrise.AssaultOps.Icarus;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.AssaultOps;

public sealed partial class AssaultOpsRuleSystem : GameRuleSystem<AssaultOpsRuleComponent>
{
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedSubdermalImplantSystem _subdermalImplant = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private UplinkSystem _uplinkSystem = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private StoreSystem _store = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private ExplosionSystem _explosions = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;

    [ValidatePrototypeId<TagPrototype>]
    private const string UplinkTagPrototype = "AssaultOpsUplink";

    [ValidatePrototypeId<AntagPrototype>]
    private const string CommanderAntagProto = "AssaultCommander";

    [ValidatePrototypeId<CurrencyPrototype>]
    private const string TelecrystalCurrencyPrototype = "Telecrystal";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<IcarusTerminalSystem.IcarusActivatedEvent>(OnIcarusActivated);
        SubscribeLocalEvent<AssaultOperativeComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<AssaultOpsRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntSelected);
        SubscribeLocalEvent<AssaultOpsRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
    }

    protected override void Started(EntityUid uid,
        AssaultOpsRuleComponent component,
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

        if (GameTicker.RunLevel == GameRunLevel.InRound)
        {
            InsertIcarusKeys(uid, component);
        }
    }

    protected override void Ended(EntityUid uid, AssaultOpsRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        var icarusKeysQuery = EntityQueryEnumerator<IcarusKeyComponent>();
        while (icarusKeysQuery.MoveNext(out var icarusKeyUid, out _))
        {
            QueueDel(icarusKeyUid);
        }
    }

    private void OnRuleLoadedGrids(Entity<AssaultOpsRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        var query = EntityQueryEnumerator<AssaultOpsShuttleComponent>();
        while (query.MoveNext(out var uid, out var shuttle))
        {
            if (Transform(uid).MapID == args.Map)
            {
                shuttle.AssociatedRule = ent;
                break;
            }
        }
    }

    private void OnAfterAntagEntSelected(Entity<AssaultOpsRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var target = (ent.Comp.TargetStation is not null) ? Name(ent.Comp.TargetStation.Value) : "the target";

        _antag.SendBriefing(args.Session,
            Loc.GetString("assaultops-welcome",
                ("station", target),
                ("name", Name(ent))),
            Color.Red,
            ent.Comp.GreetSoundNotification);

        ent.Comp.RoundstartOperatives += 1;

        if (args.Def.PrefRoles.Contains(CommanderAntagProto))
        {
            var uplink = SetupUplink(args.EntityUid, ent.Comp);
            ent.Comp.UplinkEnt = uplink;

            if (uplink == null)
                return;

            var totalTc = ent.Comp.TCAmountPerOperative * ent.Comp.RoundstartOperatives;
            var store = EnsureComp<StoreComponent>(uplink.Value);
            _store.TryAddCurrency(
                new Dictionary<string, FixedPoint2> { { TelecrystalCurrencyPrototype, totalTc } },
                uplink.Value,
                store);
        }

        else if (ent.Comp.UplinkEnt != null)
        {
            var giveTcCount = ent.Comp.TCAmountPerOperative;
            var store = EnsureComp<StoreComponent>(ent.Comp.UplinkEnt.Value);
            _store.TryAddCurrency(
                new Dictionary<string, FixedPoint2> { { TelecrystalCurrencyPrototype, giveTcCount } },
                ent.Comp.UplinkEnt.Value,
                store);
        }
    }

    private EntityUid? SetupUplink(EntityUid user, AssaultOpsRuleComponent rule)
    {
        var uplink = _uplinkSystem.FindUplinkByTag(user, UplinkTagPrototype);
        if (uplink == null)
            return null;

        _uplinkSystem.SetSunriseUplink(user, uplink.Value, 0, true);
        return uplink;
    }

    private void InsertIcarusKeys(EntityUid uid, AssaultOpsRuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var query = EntityQueryEnumerator<CommandStaffComponent>();
        while (query.MoveNext(out var ent, out var mind))
        {
            var haveKey = false;
            if (_container.TryGetContainer(ent, ImplanterComponent.ImplantSlotId, out var implantContainer))
            {
                foreach (var implant in implantContainer.ContainedEntities)
                {
                    if (MetaData(implant).EntityPrototype!.ID == component.IcarusKeyImplant)
                        haveKey = true;
                }
            }
            if (!haveKey)
                InsertKey(ent, component.IcarusKeyImplant);
        }
    }

    private bool InsertKey(EntityUid uid, string icarusKeyImplant)
    {
        var ownedCoords = Transform(uid).Coordinates;
        var implant = Spawn(icarusKeyImplant, ownedCoords);

        if (!TryComp<SubdermalImplantComponent>(implant, out var implantComp))
            return false;

        _subdermalImplant.ForceImplant(uid, (implant, implantComp));
        return true;
    }

    private void OnIcarusActivated(IcarusTerminalSystem.IcarusActivatedEvent ev)
    {
        var query = EntityQueryEnumerator<AssaultOpsRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var assaultops, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
            {
                Logger.Info("AssaultopsRule not added");
                continue;
            }
            assaultops.WinConditions.Add(WinCondition.IcarusActivated);
        }
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        var query = EntityQueryEnumerator<AssaultOpsRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var assaultops, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            var session = ev.Player;

            var mind = _mind.GetMind(session.UserId);

            if (mind == null)
                continue;

            if (HasComp<CommandStaffComponent>(ev.Mob))
                InsertKey(ev.Mob, assaultops.IcarusKeyImplant);
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New is not GameRunLevel.PostRound)
            return;

        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var rule, out _))
        {
            OnRoundEnd(uid, rule);
        }
    }

    private void OnRoundEnd(EntityUid uid, AssaultOpsRuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var total = 0;
        var alive = 0;
        foreach (var (_, state) in EntityQuery<AssaultOperativeComponent, MobStateComponent>())
        {
            total++;
            if (state.CurrentState != MobState.Alive)
                continue;

            alive++;
            break;
        }

        var allAlive = alive == total;
        if (allAlive)
        {
            if (component.WinConditions.Contains(WinCondition.IcarusActivated))
            {
                component.WinType = WinType.OpsMajor;
            }
            else
            {
                component.WinType = WinType.OpsMinor;
                component.WinConditions.Add(WinCondition.AllOpsAlive);
            }
        }
        else if (alive == 0)
        {
            if (component.WinConditions.Contains(WinCondition.IcarusActivated))
            {
                component.WinType = WinType.Hearty;
            }
            else
            {
                component.WinType = WinType.CrewMajor;
                component.WinConditions.Add(WinCondition.AllOpsDead);
            }
        }
        else
        {
            if (component.WinConditions.Contains(WinCondition.IcarusActivated))
            {
                component.WinType = WinType.OpsMinor;
            }
            else
            {
                component.WinType = WinType.Stalemate;
                component.WinConditions.Add(WinCondition.SomeOpsAlive);
            }
        }
    }

    private void OnMobStateChanged(EntityUid uid, AssaultOperativeComponent component, MobStateChangedEvent ev)
    {
        if(ev.NewMobState == MobState.Dead)
            CheckRoundShouldEnd();
    }

    private void CheckRoundShouldEnd()
    {
        var query = EntityQueryEnumerator<AssaultOpsRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var assaultops, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            if (assaultops.WinType == WinType.CrewMajor || assaultops.WinType == WinType.OpsMajor)
                continue;

            var operativesAlive = false;
            var operatives = EntityQuery<AssaultOperativeComponent, MobStateComponent>(true);
            foreach (var (assaultOp, mobState) in operatives)
            {
                if (mobState.CurrentState is MobState.Alive)
                {
                    operativesAlive = true;
                }
            }

            if (operativesAlive)
                continue;

            assaultops.WinType = WinType.CrewMajor;
            assaultops.WinConditions.Add(WinCondition.AllOpsDead);

            var shuttle = GetShuttle(uid);
            if (shuttle != null)
            {
                if (TryComp<MapGridComponent>(shuttle.Value, out var grid))
                {
                    var centerLocal = grid.LocalAABB.Center;
                    var epicenter = _transformSystem.ToMapCoordinates(new EntityCoordinates(shuttle.Value, centerLocal));
                    _explosions.QueueExplosion(epicenter, "DemolitionCharge", 30000f, 5f, 100f, cause: shuttle.Value);
                }
                else
                {
                    _explosions.QueueExplosion(shuttle.Value, "DemolitionCharge", 30000f, 5f, 100f);
                }
            }

            if (TryComp<RuleGridsComponent>(uid, out var ruleGrids) && ruleGrids.Map != null)
            {
                _mapSystem.DeleteMap(ruleGrids.Map.Value);
            }

            GameTicker.EndGameRule(uid);
        }
    }

    protected override void AppendRoundEndText(EntityUid uid,
        AssaultOpsRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        var winText = Loc.GetString($"assaultops-{component.WinType.ToString().ToLower()}");
        args.AddLine(winText);

        foreach (var cond in component.WinConditions)
        {
            var text = Loc.GetString($"assaultops-cond-{cond.ToString().ToLower()}");
            args.AddLine(text);
        }

        args.AddLine(Loc.GetString("assaultops-list-start"));

        var antags =_antag.GetAntagIdentifiers(uid);

        foreach (var (_, sessionData, name) in antags)
        {
            args.AddLine(Loc.GetString("assaultops-list-name", ("name", name), ("user", sessionData.UserName)));
        }
    }

    private EntityUid? GetShuttle(EntityUid ruleUid)
    {
        var query = EntityQueryEnumerator<AssaultOpsShuttleComponent>();
        while (query.MoveNext(out var uid, out var shuttle))
        {
            if (shuttle.AssociatedRule == ruleUid)
                return uid;
        }
        return null;
    }
}

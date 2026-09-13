using System.Linq;
using Content.Server._Sunrise.BloodCult.Objectives.Components;
using Content.Server._Sunrise.BloodCult.Objectives.Systems;
using Content.Server._Sunrise.BloodCult.Runes.Systems;
using Content.Server._Sunrise.TraitorTarget;
using Content.Server.Antag;
using Content.Server.Bed.Cryostorage;
using Content.Server.Bible.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Sunrise.BloodCult;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.CollectiveMind;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Clumsy;
using Content.Shared.GameTicking.Components;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.BloodCult.GameRule;

public sealed partial class BloodCultRuleSystem : GameRuleSystem<BloodCultRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antagSelection = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private NpcFactionSystem _factionSystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RoundEndSystem _roundEndSystem = default!;
    [Dependency] private StorageSystem _storageSystem = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    [Dependency] private KillCultistTargetsConditionSystem _cultistTargetsConditionSystem = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private SunriseHumanoidBodySystem _sunriseBody = default!;

    private static readonly ProtoId<TagPrototype> CultistTag = "Cultist";
    private static readonly ProtoId<TagPrototype> DeconvertedCultistTag = "DeconvertedCultist";
    private static readonly ProtoId<NpcFactionPrototype> BloodCultFaction = "BloodCult";
    private static readonly ProtoId<NpcFactionPrototype> NanoTrasenFaction = "NanoTrasen";

    private readonly EntProtoId _mindRoleCultistPrototypeId = "MindRoleCultist";
    private readonly EntProtoId _cultistKillObjective = "CultistKillObjective";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultNarsieSummoned>(OnNarsieSummon);
        SubscribeLocalEvent<UpdateCultAppearance>(UpdateCultistsAppearance);

        SubscribeLocalEvent<BloodCultistComponent, ComponentInit>(OnCultistComponentInit);
        SubscribeLocalEvent<BloodCultistComponent, ComponentRemove>(OnCultistComponentRemoved);
        SubscribeLocalEvent<BloodCultistComponent, MobStateChangedEvent>(OnCultistsStateChanged);
        SubscribeLocalEvent<BloodCultTargetComponent, ComponentShutdown>(OnCultTargetDeleted);
        SubscribeLocalEvent<BloodCultTargetComponent, CryostorageEnteredEvent>(OnCultTargetEnterCryo);

        SubscribeLocalEvent<BloodCultRuleComponent, AfterAntagEntitySelectedEvent>(AfterEntitySelected);
        SubscribeLocalEvent<BloodCultRuleComponent, AntagSelectionCompleteEvent>(OnAfterAntagSelectionComplete);
    }

    private void OnCultTargetEnterCryo(EntityUid uid, BloodCultTargetComponent component, CryostorageEnteredEvent args)
    {
        var rule = GetRule();
        if (rule == null)
            return;

        // Логика уровня /b: Если цель культа попадет в крио, значит эту цель надо заменить.

        if (rule.CultTargets.ContainsKey(uid))
        {
            rule.CultTargets.Remove(uid);
            RemCompDeferred<BloodCultTargetComponent>(uid);
            AddCultTarget(rule);
        }
    }


    private void OnCultTargetDeleted(EntityUid uid, BloodCultTargetComponent component, ComponentShutdown args)
    {
        var rule = GetRule();
        if (rule == null)
            return;

        // Логика уровня /b: Если цель культа будет удалена, значит эту цель надо заменить.
        // Жертвоприношение не будет считаться удалением

        if (rule.CultTargets.ContainsKey(uid) && rule.CultTargets[uid] == false)
        {
            rule.CultTargets.Remove(uid);
            RemCompDeferred<BloodCultTargetComponent>(uid);
            AddCultTarget(rule);
        }
    }

    public void AddCultTarget(BloodCultRuleComponent rule)
    {
        var potentialTargets = FindPotentialTargets();

        potentialTargets.RemoveAll(rule.CultTargets.Keys.Contains);

        var priorityTargets = potentialTargets
            .Where(target =>
                HasComp<BibleUserComponent>(target) ||
                HasComp<MindShieldComponent>(target))
            .ToList();

        potentialTargets.RemoveAll(priorityTargets.Contains);

        var selectedVictims = SelectRandomTargets(priorityTargets, 1);
        selectedVictims.AddRange(SelectRandomTargets(potentialTargets, 1 - selectedVictims.Count));

        foreach (var selectedVictim in selectedVictims)
        {
            rule.CultTargets.Add(selectedVictim, false);
        }

        var query = EntityQueryEnumerator<KillCultistTargetsConditionComponent>();

        while (query.MoveNext(out var uid, out var killCultistTargetsComponent))
        {
            _cultistTargetsConditionSystem.RefresTitle(uid, rule.CultTargets, killCultistTargetsComponent);
        }
    }

    /// <summary>
    /// Manually add a specific entity as a cult target.
    /// </summary>
    public bool AddSpecificCultTarget(EntityUid target, BloodCultRuleComponent rule)
    {
        if (!Exists(target) || rule.CultTargets.ContainsKey(target))
            return false;

        rule.CultTargets.Add(target, false);
        EnsureComp<BloodCultTargetComponent>(target);

        var query = EntityQueryEnumerator<KillCultistTargetsConditionComponent>();
        while (query.MoveNext(out var uid, out var killCultistTargetsComponent))
        {
            _cultistTargetsConditionSystem.RefresTitle(uid, rule.CultTargets, killCultistTargetsComponent);
        }
        return true;
    }

    /// <summary>
    /// Manually remove a specific entity as a cult target.
    /// </summary>
    public bool RemoveSpecificCultTarget(EntityUid target, BloodCultRuleComponent rule)
    {
        if (!rule.CultTargets.ContainsKey(target))
            return false;

        rule.CultTargets.Remove(target);
        if (Exists(target))
            RemComp<BloodCultTargetComponent>(target);

        var query = EntityQueryEnumerator<KillCultistTargetsConditionComponent>();
        while (query.MoveNext(out var uid, out var killCultistTargetsComponent))
        {
            _cultistTargetsConditionSystem.RefresTitle(uid, rule.CultTargets, killCultistTargetsComponent);
        }
        return true;
    }

    protected override void Added(EntityUid uid,
        BloodCultRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);
        var cultTypes = Enum.GetValues(typeof(BloodCultType)).Cast<BloodCultType>().ToArray();
        component.CultType = _random.Pick(cultTypes);
    }

    protected override void AppendRoundEndText(EntityUid uid,
        BloodCultRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        var winText = Loc.GetString($"cult-cond-{component.WinCondition.ToString().ToLower()}");
        args.AddLine(winText);

        args.AddLine(Loc.GetString("cultists-list-start"));

        var antags = _antagSelection.GetAntagIdentifiers(uid);

        foreach (var (_, sessionData, name) in antags)
        {
            var lising = Loc.GetString("cultists-list-name", ("name", name), ("user", sessionData.UserName));
            args.AddLine(lising);
        }
    }

    private void AfterEntitySelected(Entity<BloodCultRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        Log.Debug($"AfterAntagEntitySelected {ToPrettyString(ent)}");
        MakeCultist(args.EntityUid, ent.Comp);
    }

    private void OnCultistsStateChanged(EntityUid uid, BloodCultistComponent component, MobStateChangedEvent ev)
    {
        if (ev.NewMobState == MobState.Dead)
        {
            CheckRoundShouldEnd();
        }
    }

    public BloodCultRuleComponent? GetRule()
    {
        var rule = EntityQuery<BloodCultRuleComponent>().FirstOrDefault();
        return rule;
    }

    public void ChangeSacrificeCount(BloodCultRuleComponent rule, int count)
    {
        rule.SacrificeCount = count;
    }

    private void OnAfterAntagSelectionComplete(Entity<BloodCultRuleComponent> ent, ref AntagSelectionCompleteEvent args)
    {
        var potentialTargets = FindPotentialTargets();

        var priorityTargets = potentialTargets
            .Where(target =>
                HasComp<BibleUserComponent>(target) ||
                HasComp<MindShieldComponent>(target))
                .ToList();

        potentialTargets.RemoveAll(priorityTargets.Contains);

        var numTargets = MathHelper.Clamp(
            _playerManager.PlayerCount / ent.Comp.TargetsPerPlayer,
            ent.Comp.MinTargets,
            ent.Comp.MaxTargets);

        var selectedVictims = SelectRandomTargets(priorityTargets, numTargets);
        selectedVictims.AddRange(SelectRandomTargets(potentialTargets, numTargets - selectedVictims.Count));

        foreach (var selectedVictim in selectedVictims)
        {
            ent.Comp.CultTargets.Add(selectedVictim, false);
            EnsureComp<BloodCultTargetComponent>(selectedVictim);
        }

        var query = EntityQueryEnumerator<KillCultistTargetsConditionComponent>();

        while (query.MoveNext(out var uid, out var killCultistTargetsComponent))
        {
            _cultistTargetsConditionSystem.RefresTitle(uid, ent.Comp.CultTargets, killCultistTargetsComponent);
        }
    }

    private List<EntityUid> SelectRandomTargets(List<EntityUid> targets, int count)
    {
        _random.Shuffle(targets);
        return targets.Take(count).ToList();
    }

    public bool CultAwakened()
    {
        var querry = EntityQueryEnumerator<BloodCultRuleComponent, GameRuleComponent>();

        while (querry.MoveNext(out _, out var cultRuleComponent, out _))
        {
            var cultists = new List<EntityUid>();
            var cultisQuery = EntityQueryEnumerator<BloodCultistComponent>();
            while (cultisQuery.MoveNext(out var cultistUid, out _))
            {
                cultists.Add(cultistUid);
            }

            var constructs = new List<EntityUid>();
            var constructQuery = EntityQueryEnumerator<ConstructComponent>();
            while (constructQuery.MoveNext(out var constructUid, out _))
            {
                constructs.Add(constructUid);
            }

            var totalPlayers = _playerManager.PlayerCount;
            var pentagramThreshold = totalPlayers * cultRuleComponent.PentagramThresholdPercentage / 100;
            var totalCultMembers = cultists.Count + constructs.Count;

            if (totalCultMembers > pentagramThreshold)
            {
                return true;
            }
        }

        return false;
    }

    public bool TargetsKill()
    {
        var query = EntityQueryEnumerator<BloodCultRuleComponent, GameRuleComponent>();

        while (query.MoveNext(out _, out var cultRuleComponent, out _))
        {
            if (cultRuleComponent.CultTargets.Values.All(value => value))
            {
                return true;
            }
        }

        return false;
    }

    private void CheckRoundShouldEnd()
    {
        var query = EntityQueryEnumerator<BloodCultRuleComponent, GameRuleComponent>();
        var aliveCultistsCount = 0;

        while (query.MoveNext(out var uid, out var cultRuleComponent, out _))
        {
            var cultisQuery = EntityQueryEnumerator<BloodCultistComponent>();
            while (cultisQuery.MoveNext(out var cultistUid, out _))
            {
                if (!HasComp<HumanoidProfileComponent>(cultistUid))
                    continue;

                if (!TryComp<MobStateComponent>(cultistUid, out var mobState))
                    continue;

                if (_mobStateSystem.IsAlive(cultistUid, mobState))
                {
                    aliveCultistsCount++;
                }
            }

            if (aliveCultistsCount != 0)
                continue;

            cultRuleComponent.WinCondition = CultWinCondition.CultFailure;
            GameTicker.EndGameRule(uid);
        }
    }

    private void OnCultistComponentInit(EntityUid uid, BloodCultistComponent component, ComponentInit args)
    {
        var ev = new UpdateCultAppearance();
        RaiseLocalEvent(ev);

        _actionsSystem.AddAction(uid, ref component.BloodMagicEntity, BloodCultistComponent.BloodMagicAction);
    }

    private void OnCultistComponentRemoved(EntityUid uid, BloodCultistComponent component, ComponentRemove args)
    {
        if (TryComp<CollectiveMindComponent>(uid, out var collectiveMind))
        {
            collectiveMind.Minds.Remove("BloodCult");

            if (collectiveMind.Minds.Count == 0)
                RemComp<CollectiveMindComponent>(uid);
        }

        if (_mindSystem.TryGetMind(uid, out var mindId, out var mind))
        {
            if (_mindSystem.TryFindObjective((mindId, mind), _cultistKillObjective, out var objective))
            {
                _mindSystem.TryRemoveObjective(mindId, mind, mind.Objectives.IndexOf(objective.Value));
            }
            _roles.MindRemoveRole<BloodCultistRoleComponent>((mindId, mind));
        }

        var query = EntityQueryEnumerator<BloodCultRuleComponent, GameRuleComponent>();

        while (query.MoveNext(out var ruleEnt, out var cultRuleComponent, out _))
        {
            if (!GameTicker.IsGameRuleAdded(ruleEnt))
                continue;

            RemoveCultistAppearance(uid);

            CheckRoundShouldEnd();
        }

        if (component.BloodMagicEntity is { } bloodMagicEntity && !TerminatingOrDeleted(bloodMagicEntity))
        {
            _actionsSystem.RemoveAction(uid, bloodMagicEntity);
            QueueDel(bloodMagicEntity);
        }

        component.BloodMagicEntity = null;
        if (TryComp<ActionsComponent>(uid, out var actionsComponent))
        {
            foreach (var userAction in actionsComponent.Actions)
            {
                if (TerminatingOrDeleted(userAction))
                    continue;

                var entityPrototypeId = MetaData(userAction).EntityPrototype?.ID;
                if (entityPrototypeId != null && BloodCultistComponent.CultistActions.Contains(entityPrototypeId))
                    _actionsSystem.RemoveAction(uid, userAction);
            }
        }
    }

    private void RemoveCultistAppearance(EntityUid cultist)
    {
        if (HasComp<HumanoidProfileComponent>(cultist))
            _sunriseBody.SetEyeColor(cultist, Color.White);

        RemComp<PentagramComponent>(cultist);
    }

    private void UpdateCultistsAppearance(UpdateCultAppearance ev)
    {
        var rule = GetRule();
        if (rule == null)
            return;

        var cultists = new List<EntityUid>();
        var cultisQuery = EntityQueryEnumerator<BloodCultistComponent>();
        while (cultisQuery.MoveNext(out var cultistUid, out _))
        {
            if (HasComp<HumanoidProfileComponent>(cultistUid))
                cultists.Add(cultistUid);
        }

        var constructs = new List<EntityUid>();
        var constructQuery = EntityQueryEnumerator<ConstructComponent>();
        while (constructQuery.MoveNext(out var constructUid, out _))
        {
            constructs.Add(constructUid);
        }

        var totalCultMembers = cultists.Count + constructs.Count;
        var totalPlayers = _playerManager.PlayerCount;

        var redEyeThreshold = totalPlayers * rule.ReadEyeThresholdPercentage / 100;
        var pentagramThreshold = totalPlayers * rule.PentagramThresholdPercentage / 100;

        if (totalCultMembers < redEyeThreshold)
            return;

        foreach (var cultist in cultists)
        {
            if (HasComp<HumanoidProfileComponent>(cultist))
                _sunriseBody.SetEyeColor(cultist, rule.EyeColor);

            if (totalCultMembers < pentagramThreshold)
                return;

            EnsureComp<PentagramComponent>(cultist);
        }
    }

    private List<EntityUid> FindPotentialTargets()
    {
        var potentialTargets = new List<EntityUid>();

        var query = EntityQueryEnumerator<MindContainerComponent, AntagTargetComponent, HumanoidProfileComponent>();
        while (query.MoveNext(out var uid, out var mind, out _, out _))
        {
            if (mind.Mind == null || HasComp<BloodCultistComponent>(uid))
                continue;

            potentialTargets.Add(uid);
        }

        return potentialTargets;
    }

    public bool MakeCultist(EntityUid cultist, BloodCultRuleComponent rule)
    {
        if (!_mindSystem.TryGetMind(cultist, out var mindId, out var mind))
            return false;

        if (_tagSystem.HasTag(cultist, DeconvertedCultistTag))
            return false;

        _roles.MindAddRole(mindId, _mindRoleCultistPrototypeId);

        var isHumanoid = HasComp<HumanoidProfileComponent>(cultist);

        var cultistComponent = EnsureComp<BloodCultistComponent>(cultist);

        if (HasComp<ClumsyComponent>(cultist))
            RemComp<ClumsyComponent>(cultist);

        EnsureComp<CultMemberComponent>(cultist);

        var collectiveMind = EnsureComp<CollectiveMindComponent>(cultist);
        collectiveMind.Minds.Add("BloodCult");

        _tagSystem.AddTag(cultist, CultistTag);

        _factionSystem.RemoveFaction(cultist, NanoTrasenFaction, false);
        _factionSystem.AddFaction(cultist, BloodCultFaction);

        // Для животных нужно добавить компонент StatusIcon, чтобы показывать иконку культиста
        if (!isHumanoid && !HasComp<StatusIconComponent>(cultist))
            EnsureComp<StatusIconComponent>(cultist);

        if (rule.CultType == null ||
            !Proto.TryIndex<BloodCultPrototype>($"{rule.CultType.Value.ToString()}Cult", out var cultPrototype))
            return false;

        cultistComponent.CultType = rule.CultType;

        if (isHumanoid)
        {
            _inventorySystem.TryGetSlotEntity(cultist, "back", out var backPack);

            foreach (var itemPrototype in cultPrototype.StartingItems)
            {
                var itemEntity = Spawn(itemPrototype, Transform(cultist).Coordinates);

                if (backPack != null)
                {
                    _storageSystem.Insert(backPack.Value, itemEntity, out _);
                }
            }

            if (_playerManager.TryGetSessionById(mind.UserId, out var session))
            {
                _audioSystem.PlayGlobal(rule.GreatingsSound,
                    Filter.Empty().AddPlayer(session),
                    false,
                    AudioParams.Default);

                _chatManager.DispatchServerMessage(session, Loc.GetString("cult-role-greeting"));
            }

            _mindSystem.TryAddObjective(mindId, mind, "CultistKillObjective");
        }

        Dirty(cultist, cultistComponent);

        return true;
    }

    private void OnNarsieSummon(CultNarsieSummoned ev)
    {
        var query = EntityQuery<MobStateComponent, MindContainerComponent, BloodCultistComponent>().ToList();

        foreach (var (mobState, mindContainer, _) in query)
        {
            if (!mindContainer.HasMind || mindContainer.Mind is null)
            {
                continue;
            }

            var reaper = Spawn(BloodCultSystem.ReaperConstructPrototypeId, Transform(mobState.Owner).Coordinates);
            _mindSystem.TransferTo(mindContainer.Mind.Value, reaper);

            _gibbing.Gib(mobState.Owner);
        }

        _roundEndSystem.ForceSetCountdown(TimeSpan.FromSeconds(10), cantRecall: true);
    }
}

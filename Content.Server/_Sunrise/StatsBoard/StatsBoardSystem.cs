using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Shared._Sunrise.StatsBoard;
using Content.Shared.Bed.Sleep;
using Content.Shared.Cargo.Components;
using Content.Shared.Clumsy;
using Content.Shared.Construction;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Doors.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Fluids;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Slippery;
using Content.Shared.Tag;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Localization;
using Content.Shared.Damage.Systems;

namespace Content.Server.StatsBoard;

public sealed partial class StatsBoardSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    private static readonly ProtoId<TagPrototype> HamsterTag = "Hamster";
    private static readonly ProtoId<TagPrototype> MouseTag = "Mouse";

    private (EntityUid? killer, EntityUid? victim, TimeSpan time) _firstMurder = (null, null, TimeSpan.Zero);
    private EntityUid? _hamsterKiller;
    private int _jointCreated;
    private (EntityUid? clown, TimeSpan? time) _clownCuffed = (null, null);
    private readonly Dictionary<EntityUid, StatisticEntry> _statisticEntries = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActorComponent, DamageChangedEvent>(OnDamageModify);
        SubscribeLocalEvent<ActorComponent, SlippedEvent>(OnSlippedEvent);
        SubscribeLocalEvent<ActorComponent, CreamedEvent>(OnCreamedEvent);
        SubscribeLocalEvent<ActorComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<ActorComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ActorComponent, DoorEmaggedEvent>(OnDoorEmagged);
        SubscribeLocalEvent<ActorComponent, ElectrocutedEvent>(OnElectrocuted);
        SubscribeLocalEvent<ActorComponent, SubtractCashEvent>(OnItemPurchasedEvent);
        SubscribeLocalEvent<ActorComponent, CuffedEvent>(OnCuffedEvent);
        SubscribeLocalEvent<ActorComponent, ItemConstructionCreated>(OnCraftedEvent);
        SubscribeLocalEvent<ActorComponent, AbsorberPudleEvent>(OnAbsorbedPuddleEvent);
        SubscribeLocalEvent<ActorComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMindAdded(EntityUid uid, ActorComponent comp, MindAddedMessage ev)
    {
        if (_statisticEntries.ContainsKey(uid) || ev.Mind.Comp.UserId == null || HasComp<GhostComponent>(uid))
            return;

        var value = new StatisticEntry(MetaData(uid).EntityName, ev.Mind.Comp.UserId.Value);
        _statisticEntries.Add(uid, value);
    }

    public void CleanEntries()
    {
        _firstMurder = (null, null, TimeSpan.Zero);
        _hamsterKiller = null;
        _jointCreated = 0;
        _clownCuffed = (null, TimeSpan.Zero);
        _statisticEntries.Clear();
    }

    private void OnAbsorbedPuddleEvent(EntityUid uid, ActorComponent comp, ref AbsorberPudleEvent ev)
    {
        if (!_mindSystem.TryGetMind(comp.PlayerSession, out var mindId, out var mind))
            return;

        if (_statisticEntries.TryGetValue(uid, out var value))
        {
            value.AbsorbedPuddleCount += 1;
        }
    }

    private void OnCraftedEvent(EntityUid uid, ActorComponent comp, ref ItemConstructionCreated ev)
    {
        if (!_mindSystem.TryGetMind(comp.PlayerSession, out var mindId, out var mind))
            return;

        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        if (!TryComp(ev.Item, out MetaDataComponent? metaDataComponent))
            return;

        if (metaDataComponent.EntityPrototype == null)
            return;
        switch (metaDataComponent.EntityPrototype.ID)
        {
            case "Blunt":
            case "Joint":
                _jointCreated += 1;
                break;
        }
    }

    private void OnCuffedEvent(EntityUid uid, ActorComponent comp, ref CuffedEvent ev)
    {
        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        value.CuffedCount += 1;
        if (_clownCuffed.clown != null)
            return;
        if (!HasComp<ClumsyComponent>(uid))
            return;
        _clownCuffed.clown = uid;
        _clownCuffed.time = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
    }

    private void OnItemPurchasedEvent(EntityUid uid, ActorComponent comp, ref SubtractCashEvent ev)
    {
        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        if (ev.Currency != "Telecrystal")
            return;
        if (value.SpentTk == null)
        {
            value.SpentTk = ev.Cost.Int();
        }
        else
        {
            value.SpentTk += ev.Cost.Int();
        }
    }

    private void OnElectrocuted(EntityUid uid, ActorComponent comp, ElectrocutedEvent ev)
    {
        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        value.ElectrocutedCount += 1;
    }

    private void OnDoorEmagged(EntityUid uid, ActorComponent comp, ref DoorEmaggedEvent ev)
    {
        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        value.DoorEmagedCount += 1;
    }

    private void OnInteractionAttempt(EntityUid uid, ActorComponent comp, InteractionAttemptEvent args)
    {
        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        if (!HasComp<ItemComponent>(args.Target))
            return;
        if (MetaData(args.Target.Value).EntityPrototype == null)
            return;
        var entityPrototype = MetaData(args.Target.Value).EntityPrototype;
        if (entityPrototype is not { ID: "CaptainIDCard" })
            return;
        if (value.IsInteractedCaptainCard)
            return;
        value.IsInteractedCaptainCard = true;
    }

    private void OnCreamedEvent(EntityUid uid, ActorComponent comp, ref CreamedEvent ev)
    {
        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        value.CreamedCount += 1;
    }

    private void OnMobStateChanged(EntityUid uid, ActorComponent comp, MobStateChangedEvent args)
    {
        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        switch (args.NewMobState)
        {
            case MobState.Dead:
            {
                value.DeadCount += 1;

                EntityUid? origin = null;
                if (args.Origin != null)
                {
                    origin = args.Origin.Value;
                }

                if (_firstMurder.victim == null && HasComp<HumanoidProfileComponent>(uid))
                {
                    _firstMurder.victim = uid;
                    _firstMurder.killer = origin;
                    _firstMurder.time = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                    Logger.Info($"First Murder. CurTime: {_gameTiming.CurTime}, RoundStartTimeSpan: {_gameTicker.RoundStartTimeSpan}, Substract: {_gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan)}");
                }

                if (origin != null)
                {
                    if (_hamsterKiller == null && _tagSystem.HasTag(uid, HamsterTag))
                    {
                        _hamsterKiller = origin.Value;
                    }

                    if (!_statisticEntries.TryGetValue(origin.Value, out var originEntry))
                        return;

                    if (_tagSystem.HasTag(uid, MouseTag))
                    {
                        originEntry.KilledMouseCount += 1;
                    }

                    if (HasComp<HumanoidProfileComponent>(uid))
                        originEntry.HumanoidKillCount += 1;
                }

                break;
            }
        }
    }

    private void OnDamageModify(EntityUid uid, ActorComponent comp, DamageChangedEvent ev)
    {
        DamageGetModify(uid, ev);

        if (ev.Origin != null)
            DamageTakeModify(ev.Origin.Value, ev);
    }

    private void DamageTakeModify(EntityUid uid, DamageChangedEvent ev)
    {
        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        if (ev.DamageDelta == null)
            return;

        if (ev.DamageIncreased)
        {
            value.TotalInflictedDamage += ev.DamageDelta.GetTotal().Int();
        }
        else
        {
            value.TotalInflictedHeal += Math.Abs(ev.DamageDelta.GetTotal().Int());
        }
    }

    private void DamageGetModify(EntityUid uid, DamageChangedEvent ev)
    {
        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        if (ev.DamageDelta == null)
            return;

        if (ev.DamageIncreased)
        {
            value.TotalTakeDamage += ev.DamageDelta.GetTotal().Int();
        }
        else
        {
            value.TotalTakeHeal += Math.Abs(ev.DamageDelta.GetTotal().Int());
        }
    }

    private void OnSlippedEvent(EntityUid uid, ActorComponent comp, ref SlippedEvent ev)
    {
        if (!_statisticEntries.TryGetValue(uid, out var value))
            return;

        if (HasComp<HumanoidProfileComponent>(uid))
            value.SlippedCount += 1;
    }

    private StationBankAccountComponent? GetBankAccount(EntityUid? uid)
    {
        if (uid != null && TryComp<StationBankAccountComponent>(uid, out var bankAccount))
        {
            return bankAccount;
        }
        return null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var statsQuery = EntityQueryEnumerator<ActorComponent>();
        while (statsQuery.MoveNext(out var ent, out var comp))
        {
            if (!_statisticEntries.TryGetValue(ent, out var value))
                return;

            if (TryComp(ent, out TransformComponent? transformComponent) &&
                transformComponent.GridUid == null && HasComp<HumanoidProfileComponent>(ent))
                value.SpaceTime += TimeSpan.FromSeconds(frameTime);

            if (TryComp<CuffableComponent>(ent, out var cuffableComponent) &&
                !cuffableComponent.CanStillInteract)
                value.CuffedTime += TimeSpan.FromSeconds(frameTime);

            if (HasComp<SleepingComponent>(ent))
                value.SleepTime += TimeSpan.FromSeconds(frameTime);
        }
    }

    public StatisticEntry[] GetStatisticEntries()
    {
        return _statisticEntries.Values.ToArray();
    }

    public SharedStatisticEntry ConvertToSharedStatisticEntry(StatisticEntry entry)
    {
        return new SharedStatisticEntry(entry.Name, entry.FirstActor)
        {
            TotalTakeDamage = entry.TotalTakeDamage,
            TotalTakeHeal = entry.TotalTakeHeal,
            TotalInflictedDamage = entry.TotalInflictedDamage,
            TotalInflictedHeal = entry.TotalInflictedHeal,
            SlippedCount = entry.SlippedCount,
            CreamedCount = entry.CreamedCount,
            DoorEmagedCount = entry.DoorEmagedCount,
            ElectrocutedCount = entry.ElectrocutedCount,
            CuffedCount = entry.CuffedCount,
            AbsorbedPuddleCount = entry.AbsorbedPuddleCount,
            SpentTk = entry.SpentTk,
            DeadCount = entry.DeadCount,
            HumanoidKillCount = entry.HumanoidKillCount,
            KilledMouseCount = entry.KilledMouseCount,
            CuffedTime = entry.CuffedTime,
            SpaceTime = entry.SpaceTime,
            SleepTime = entry.SleepTime,
            IsInteractedCaptainCard = entry.IsInteractedCaptainCard,
        };
    }

    public string GetRoundStats()
    {
        var result = "";
        var totalSlipped = 0;
        var totalCreampied = 0;
        var totalDamage = 0;
        var totalHeal = 0;
        var totalDoorEmaged = 0;
        var maxSlippedCount = 0;
        var maxDeadCount = 0;
        var maxSpeciesCount = 0;
        var maxDoorEmagedCount = 0;
        var totalKilledMice = 0;
        var totalAbsorbedPuddle = 0;
        var maxKillsMice = 0;
        var totalCaptainCardInteracted = 0;
        var totalElectrocutedCount = 0;
        var totalSleepTime = TimeSpan.Zero;
        var minSpentTk = int.MaxValue;
        var maxHumKillCount = 0;
        var totalCuffedCount = 0;
        var maxTakeDamage = 0;
        var maxInflictedHeal = 0;
        var maxInflictedDamage = 0;
        var maxPuddleAbsorb = 0;
        var maxCuffedTime = TimeSpan.Zero;
        var maxSpaceTime = TimeSpan.Zero;
        var maxSleepTime = TimeSpan.Zero;
        string? mostPopularSpecies = null;
        Dictionary<string, int> roundSpecies = new();
        EntityUid? mostSlippedCharacter = null;
        EntityUid? mostDeadCharacter = null;
        EntityUid? mostDoorEmagedCharacter = null;
        EntityUid? mostKillsMiceCharacter = null;
        EntityUid? playerWithMinSpentTk = null;
        EntityUid? playerWithMaxHumKills = null;
        EntityUid? playerWithMaxDamage = null;
        EntityUid? playerWithLongestCuffedTime = null;
        EntityUid? playerWithLongestSpaceTime = null;
        EntityUid? playerWithLongestSleepTime = null;
        EntityUid? playerWithMostInflictedHeal = null;
        EntityUid? playerWithMostInflictedDamage = null;
        EntityUid? playerWithMostPuddleAbsorb = null;

        foreach (var (uid, data) in _statisticEntries)
        {
            if (TryComp<HumanoidProfileComponent>(uid, out var humanoidAppearanceComponent))
            {
                var speciesProto = _prototypeManager.Index<SpeciesPrototype>(humanoidAppearanceComponent.Species);

                if (roundSpecies.TryGetValue(speciesProto.Name, out var count))
                {
                    roundSpecies[speciesProto.Name] = count + 1;
                }
                else
                {
                    roundSpecies.Add(speciesProto.Name, 1);
                }
            }

            totalDoorEmaged += data.DoorEmagedCount;
            totalSlipped += data.SlippedCount;
            totalCreampied += data.CreamedCount;
            totalDamage += data.TotalTakeDamage;
            totalHeal += data.TotalTakeHeal;
            totalCuffedCount += data.CuffedCount;
            totalKilledMice += data.KilledMouseCount;
            totalSleepTime += data.SleepTime;
            totalAbsorbedPuddle += data.AbsorbedPuddleCount;
            totalElectrocutedCount += data.ElectrocutedCount;

            if (data.SlippedCount > maxSlippedCount)
            {
                maxSlippedCount = data.SlippedCount;
                mostSlippedCharacter = uid;
            }

            if (data.DoorEmagedCount > maxDoorEmagedCount)
            {
                maxDoorEmagedCount = data.DoorEmagedCount;
                mostDoorEmagedCharacter = uid;
            }

            if (data.DeadCount > maxDeadCount)
            {
                maxDeadCount = data.DeadCount;
                mostDeadCharacter = uid;
            }

            if (data.KilledMouseCount > maxKillsMice)
            {
                maxKillsMice = data.KilledMouseCount;
                mostKillsMiceCharacter = uid;
            }

            if (data.IsInteractedCaptainCard)
            {
                totalCaptainCardInteracted += 1;
            }

            if (data.SpentTk != null && data.SpentTk < minSpentTk)
            {
                minSpentTk = data.SpentTk.Value;
                playerWithMinSpentTk = uid;
            }

            if (data.HumanoidKillCount > maxHumKillCount)
            {
                maxHumKillCount = data.HumanoidKillCount;
                playerWithMaxHumKills = uid;
            }

            if (data.TotalTakeDamage > maxTakeDamage)
            {
                maxTakeDamage = data.TotalTakeDamage;
                playerWithMaxDamage = uid;
            }

            if (data.CuffedTime > maxCuffedTime)
            {
                maxCuffedTime = data.CuffedTime;
                playerWithLongestCuffedTime = uid;
            }

            if (data.SleepTime > maxSleepTime)
            {
                maxSleepTime = data.SleepTime;
                playerWithLongestSleepTime = uid;
            }

            if (data.SpaceTime > maxSpaceTime)
            {
                maxSpaceTime = data.SpaceTime;
                playerWithLongestSpaceTime = uid;
            }

            if (data.TotalInflictedHeal > maxInflictedHeal)
            {
                maxInflictedHeal = data.TotalInflictedHeal;
                playerWithMostInflictedHeal = uid;
            }

            if (data.TotalInflictedDamage > maxInflictedDamage)
            {
                maxInflictedDamage = data.TotalInflictedDamage;
                playerWithMostInflictedDamage = uid;
            }

            if (data.AbsorbedPuddleCount > maxPuddleAbsorb)
            {
                maxPuddleAbsorb = data.AbsorbedPuddleCount;
                playerWithMostPuddleAbsorb = uid;
            }
        }

        result += Loc.GetString("statsentry-species-entry-name") + "\n";
        foreach (var speciesEntry in roundSpecies)
        {
            var species = speciesEntry.Key;
            var count = speciesEntry.Value;

            if (count > maxSpeciesCount)
            {
                maxSpeciesCount = count;
                mostPopularSpecies = species;
            }

            result += Loc.GetString("statsentry-species-entry", ("name", Loc.GetString(species)), ("count", count)) + "\n";
        }

        if (mostPopularSpecies != null)
        {
            result += Loc.GetString("statsentry-mst-pop-species", ("name", Loc.GetString(mostPopularSpecies))) + "\n";
        }

        var station = _station.GetStations().FirstOrDefault();
        var bank = GetBankAccount(station);

        if (bank != null)
        {
            result += Loc.GetString("statsentry-bank-balance-total", ("balance", bank.Accounts.Values.Sum())) + "\n";
            foreach (var (account, balance) in bank.Accounts)
            {
                result += Loc.GetString("statsentry-bank-balance-account", ("account", Loc.GetString(account)), ("balance", balance)) + "\n";
            }
        }

        if (_firstMurder.victim != null)
        {
            var victimUsername = TryGetUsername(_firstMurder.victim.Value);
            var victimName = TryGetName(_firstMurder.victim.Value);
            var victimUsernameColor = victimUsername != null ? $" ([color=gray]{victimUsername}[/color])" : "";
            result += Loc.GetString("statsentry-firth-murder", ("name", victimName), ("username", victimUsernameColor)) + "\n";
            result += Loc.GetString("statsentry-firth-murder-time", ("time", _firstMurder.time.ToString("hh\\:mm\\:ss"))) + "\n";
            if (_firstMurder.killer != null)
            {
                var killerUsername = TryGetUsername(_firstMurder.killer.Value);
                var killerName = TryGetName(_firstMurder.killer.Value);
                var killerUsernameColor = killerUsername != null ? $" ([color=gray]{killerUsername}[/color])" : "";
                result += Loc.GetString("statsentry-firth-murder-killer", ("name", killerName), ("username", killerUsernameColor)) + "\n";
            }
            else
            {
                result += Loc.GetString("statsentry-firth-murder-killer-none") + "\n";
            }
        }

        if (totalSlipped >= 1)
        {
            result += Loc.GetString("statsentry-total-slipped", ("count", totalSlipped)) + "\n";
        }

        if (mostSlippedCharacter != null && maxSlippedCount > 1)
        {
            var username = TryGetUsername(mostSlippedCharacter.Value);
            var name = TryGetName(mostSlippedCharacter.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-most-slipped", ("name", name), ("username", usernameColor), ("count", maxSlippedCount)) + "\n";
        }

        if (totalCreampied >= 1)
        {
            result += Loc.GetString("statsentry-total-creampied", ("total", totalCreampied)) + "\n";
        }

        if (mostDeadCharacter != null && maxDeadCount > 1)
        {
            var username = TryGetUsername(mostDeadCharacter.Value);
            var name = TryGetName(mostDeadCharacter.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-most-dead", ("name", name), ("username", usernameColor), ("count", maxDeadCount)) + "\n";
        }

        if (totalDoorEmaged >= 1)
        {
            result += Loc.GetString("statsentry-total-door-emaged", ("count", totalDoorEmaged)) + "\n";
        }

        if (mostDoorEmagedCharacter != null)
        {
            var username = TryGetUsername(mostDoorEmagedCharacter.Value);
            var name = TryGetName(mostDoorEmagedCharacter.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-most-door-emaged-character", ("name", name), ("username", usernameColor), ("count", maxDoorEmagedCount)) + "\n";
        }

        if (_jointCreated >= 1)
        {
            result += Loc.GetString("statsentry-joint-created", ("count", _jointCreated)) + "\n";
        }

        if (totalKilledMice >= 1)
        {
            result += Loc.GetString("statsentry-total-killed-mice", ("count", totalKilledMice)) + "\n";
        }

        if (mostKillsMiceCharacter != null && maxKillsMice > 1)
        {
            var username = TryGetUsername(mostKillsMiceCharacter.Value);
            var name = TryGetName(mostKillsMiceCharacter.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-most-kills-mice-character", ("name", name), ("username", usernameColor), ("count", maxKillsMice)) + "\n";
        }

        if (_hamsterKiller != null)
        {
            var username = TryGetUsername(_hamsterKiller.Value);
            var name = TryGetName(_hamsterKiller.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-hamster-killer", ("name", name), ("username", usernameColor)) + "\n";
        }

        if (totalCuffedCount >= 1)
        {
            result += Loc.GetString("statsentry-total-cuffed-count", ("count", totalCuffedCount)) + "\n";
        }

        if (playerWithLongestCuffedTime != null)
        {
            var username = TryGetUsername(playerWithLongestCuffedTime.Value);
            var name = TryGetName(playerWithLongestCuffedTime.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-player-with-longest-cuffed-time", ("name", name), ("username", usernameColor), ("time", maxCuffedTime.ToString("hh\\:mm\\:ss"))) + "\n";
        }

        if (totalSleepTime > TimeSpan.Zero)
        {
            result += Loc.GetString("statsentry-total-sleep-time", ("time", totalSleepTime.ToString("hh\\:mm\\:ss"))) + "\n";
        }

        if (playerWithLongestSleepTime != null)
        {
            var username = TryGetUsername(playerWithLongestSleepTime.Value);
            var name = TryGetName(playerWithLongestSleepTime.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-player-with-longest-sleep-time", ("name", name), ("username", usernameColor)) + "\n";
            result += Loc.GetString("statsentry-player-with-longest-sleep-time-time", ("time", maxSleepTime.ToString("hh\\:mm\\:ss"))) + "\n";
        }

        if (playerWithLongestSpaceTime != null)
        {
            var username = TryGetUsername(playerWithLongestSpaceTime.Value);
            var name = TryGetName(playerWithLongestSpaceTime.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-player-with-longest-space-time", ("name", name), ("username", usernameColor), ("time", maxSpaceTime.ToString("hh\\:mm\\:ss"))) + "\n";
        }

        if (_clownCuffed.clown != null && _clownCuffed.time != null)
        {
            var username = TryGetUsername(_clownCuffed.clown.Value);
            var name = TryGetName(_clownCuffed.clown.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-clown-cuffed", ("name", name), ("username", usernameColor), ("time", _clownCuffed.time.Value.ToString("hh\\:mm\\:ss"))) + "\n";
        }

        if (totalHeal >= 1)
        {
            result += Loc.GetString("statsentry-total-heal", ("count", totalHeal)) + "\n";
        }

        if (playerWithMostInflictedHeal != null)
        {
            var username = TryGetUsername(playerWithMostInflictedHeal.Value);
            var name = TryGetName(playerWithMostInflictedHeal.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-player-with-most-infected-heal", ("name", name), ("username", usernameColor), ("count", maxInflictedHeal)) + "\n";
        }

        if (totalDamage >= 1)
        {
            result += Loc.GetString("statsentry-total-damage", ("count", totalDamage)) + "\n";
        }

        if (playerWithMostInflictedDamage != null)
        {
            var username = TryGetUsername(playerWithMostInflictedDamage.Value);
            var name = TryGetName(playerWithMostInflictedDamage.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-player-with-most-infected-damage", ("name", name), ("username", usernameColor), ("count", maxInflictedDamage)) + "\n";
        }

        if (playerWithMinSpentTk != null)
        {
            var username = TryGetUsername(playerWithMinSpentTk.Value);
            var name = TryGetName(playerWithMinSpentTk.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-player-with-min-spent-tk", ("name", name), ("username", usernameColor), ("count", minSpentTk)) + "\n";
        }

        if (playerWithMaxHumKills != null && maxHumKillCount > 1)
        {
            var username = TryGetUsername(playerWithMaxHumKills.Value);
            var name = TryGetName(playerWithMaxHumKills.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-player-with-max-hum-kills", ("name", name), ("username", usernameColor)) + "\n";
            result += Loc.GetString("statsentry-player-with-max-hum-kills-count", ("count", maxHumKillCount)) + "\n";
        }

        if (playerWithMaxDamage != null)
        {
            var username = TryGetUsername(playerWithMaxDamage.Value);
            var name = TryGetName(playerWithMaxDamage.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-player-with-max-damage", ("name", name), ("username", usernameColor), ("count", maxTakeDamage)) + "\n";
        }

        if (totalAbsorbedPuddle >= 1)
        {
            result += Loc.GetString("statsentry-total-absorbed-puddle", ("count", totalAbsorbedPuddle)) + "\n";
        }

        if (playerWithMostPuddleAbsorb != null && maxPuddleAbsorb > 1)
        {
            var username = TryGetUsername(playerWithMostPuddleAbsorb.Value);
            var name = TryGetName(playerWithMostPuddleAbsorb.Value);
            var usernameColor = username != null ? $" ([color=gray]{username}[/color])" : "";
            result += Loc.GetString("statsentry-player-with-most-puddle-absorb", ("name", name), ("username", usernameColor), ("count", maxPuddleAbsorb)) + "\n";
        }

        if (totalCaptainCardInteracted >= 1)
        {
            result += Loc.GetString("statsentry-total-captain-card-interacted", ("count", totalCaptainCardInteracted)) + "\n";
        }

        if (totalElectrocutedCount >= 1)
        {
            result += Loc.GetString("statsentry-total-electrocuted-count", ("count", totalElectrocutedCount)) + "\n";
        }

        //убрал пробельчик, так как всё равно он есть при добавлении ласт строчки

        return result;
    }

    private string? TryGetUsername(EntityUid uid)
    {
        string? username = null;

        if (_mindSystem.TryGetMind(uid, out var mindId, out var mind) && _player.TryGetSessionById(mind.UserId, out var session))
        {
            username = session.Name;
        }

        return username;
    }

    private string TryGetName(EntityUid uid)
    {
        if (_statisticEntries.TryGetValue(uid, out var value))
            return value.Name;

        if (TryComp(uid, out MetaDataComponent? metaDataComponent))
            return metaDataComponent.EntityName;

        return "Кто это блядь?";
    }
}

[Serializable]
public sealed partial class StatisticEntry(string name, NetUserId userId)
{
    public string Name { get; set; } = name;
    public NetUserId FirstActor { get; set; } = userId;
    public int TotalTakeDamage { get; set; } = 0;
    public int TotalTakeHeal { get; set; } = 0;
    public int TotalInflictedDamage { get; set; } = 0;
    public int TotalInflictedHeal { get; set; } = 0;
    public int SlippedCount { get; set; } = 0;
    public int CreamedCount { get; set; } = 0;
    public int DoorEmagedCount { get; set; } = 0;
    public int ElectrocutedCount { get; set; } = 0;
    public int CuffedCount { get; set; } = 0;
    public int AbsorbedPuddleCount { get; set; } = 0;
    public int? SpentTk { get; set; } = null;
    public int DeadCount { get; set; } = 0;
    public int HumanoidKillCount { get; set; } = 0;
    public int KilledMouseCount { get; set; } = 0;
    public TimeSpan CuffedTime { get; set; } = TimeSpan.Zero;
    public TimeSpan SpaceTime { get; set; } = TimeSpan.Zero;
    public TimeSpan SleepTime { get; set; } = TimeSpan.Zero;
    public bool IsInteractedCaptainCard { get; set; } = false;
}

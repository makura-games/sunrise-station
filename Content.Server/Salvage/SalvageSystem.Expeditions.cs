using System.Linq;
using System.Threading;
using Content.Server.Salvage.Expeditions;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Content.Shared.Procedural; // Sunrise-Edit
using Content.Shared.Random.Helpers; // Sunrise-Edit
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Shuttles.Components;
using Content.Server.GameTicking; // Sunrise-Edit
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.GameStates;
using Robust.Shared.Random; // Sunrise-Edit

namespace Content.Server.Salvage;

public sealed partial class SalvageSystem
{
    /*
     * Handles setup / teardown of salvage expeditions.
     */

    private const int MissionLimit = 5; // Sunrise-Edit
    // Sunrise added start - fixed expedition console difficulty slots
    private static readonly string[] MissionDifficultySlots =
    [
        "Easy",
        "Moderate",
        "Challenging",
        "Difficult",
        "Impossible",
    ];
    // Sunrise added end

    private readonly JobQueue _salvageQueue = new();
    private readonly List<(SpawnSalvageMissionJob Job, CancellationTokenSource CancelToken)> _salvageJobs = new();
    private const double SalvageJobTime = 0.002;

    private float _cooldown;

    private void InitializeExpeditions()
    {
        SubscribeLocalEvent<SalvageExpeditionConsoleComponent, ComponentInit>(OnSalvageConsoleInit);
        SubscribeLocalEvent<SalvageExpeditionConsoleComponent, EntParentChangedMessage>(OnSalvageConsoleParent);
        SubscribeLocalEvent<SalvageExpeditionConsoleComponent, ClaimSalvageMessage>(OnSalvageClaimMessage);

        SubscribeLocalEvent<SalvageExpeditionComponent, MapInitEvent>(OnExpeditionMapInit);
        SubscribeLocalEvent<SalvageExpeditionComponent, ComponentShutdown>(OnExpeditionShutdown);
        SubscribeLocalEvent<SalvageExpeditionComponent, ComponentGetState>(OnExpeditionGetState);

        _cooldown = _configurationManager.GetCVar(CCVars.SalvageExpeditionCooldown);
        Subs.CVar(_configurationManager, CCVars.SalvageExpeditionCooldown, SetCooldownChange);
    }

    private void OnExpeditionGetState(EntityUid uid, SalvageExpeditionComponent component, ref ComponentGetState args)
    {
        args.State = new SalvageExpeditionComponentState()
        {
            Stage = component.Stage
        };
    }

    private void SetCooldownChange(float obj)
    {
        // Update the active cooldowns if we change it.
        var diff = obj - _cooldown;

        var query = AllEntityQuery<SalvageExpeditionDataComponent>();

        while (query.MoveNext(out var comp))
        {
            comp.NextOffer += TimeSpan.FromSeconds(diff);
        }

        _cooldown = obj;
    }

    private void OnExpeditionMapInit(EntityUid uid, SalvageExpeditionComponent component, MapInitEvent args)
    {
        component.SelectedSong = _audio.ResolveSound(component.Sound);
    }

    private void OnExpeditionShutdown(EntityUid uid, SalvageExpeditionComponent component, ComponentShutdown args)
    {
        component.Stream = _audio.Stop(component.Stream);

        // First wipe any disks referencing us
        var disks = AllEntityQuery<ShuttleDestinationCoordinatesComponent>();
        while (disks.MoveNext(out var disk, out var diskComp)
               && diskComp.Destination == uid)
        {
            diskComp.Destination = null;
            Dirty(disk, diskComp);
        }

        foreach (var (job, cancelToken) in _salvageJobs.ToArray())
        {
            if (job.Station == component.Station)
            {
                cancelToken.Cancel();
                _salvageJobs.Remove((job, cancelToken));
            }
        }

        if (Deleted(component.Station))
            return;

        // Finish mission
        if (TryComp<SalvageExpeditionDataComponent>(component.Station, out var data))
        {
            FinishExpedition((component.Station, data), uid);
        }
    }

    private void UpdateExpeditions()
    {
        // Sunrise-Start
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;
        // Sunrise-End

        var currentTime = _timing.CurTime;
        _salvageQueue.Process();

        foreach (var (job, cancelToken) in _salvageJobs.ToArray())
        {
            switch (job.Status)
            {
                case JobStatus.Finished:
                    _salvageJobs.Remove((job, cancelToken));
                    break;
            }
        }

        var query = EntityQueryEnumerator<SalvageExpeditionDataComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Update offers
            if (comp.NextOffer > currentTime || comp.Claimed)
                continue;

            comp.Cooldown = false;
            comp.NextOffer += TimeSpan.FromSeconds(_cooldown);
            GenerateMissions(comp);
            UpdateConsoles((uid, comp));
        }
    }

    private void FinishExpedition(Entity<SalvageExpeditionDataComponent> expedition, EntityUid uid)
    {
        var component = expedition.Comp;
        component.NextOffer = _timing.CurTime + TimeSpan.FromSeconds(_cooldown);
        Announce(uid, Loc.GetString("salvage-expedition-completed"));
        component.ActiveMission = 0;
        component.Cooldown = true;
        UpdateConsoles(expedition);
    }

    private void GenerateMissions(SalvageExpeditionDataComponent component)
    {
        component.Missions.Clear();
        // Sunrise-Start
        var roundDuration = _gameTicker.RunLevel == GameRunLevel.InRound
            ? _gameTicker.RoundDuration()
            : TimeSpan.Zero;
        var availableDifficulties = ProtoMan // Sunrise-Edit
            .EnumeratePrototypes<SalvageDifficultyPrototype>()
            .Where(d => d.Delay <= roundDuration && d.Probability > 0f)
            .ToDictionary(d => d.ID, d => d.Probability); // Sunrise-Edit

        if (availableDifficulties.Count == 0) // Sunrise-Edit
            return;
        // Sunrise-End
        for (var i = 0; i < Math.Min(MissionLimit, MissionDifficultySlots.Length); i++) // Sunrise-Edit
        {
            // Sunrise added start - fixed expedition console difficulty slots
            var slotDifficulty = MissionDifficultySlots[i];
            var difficulty = availableDifficulties.ContainsKey(slotDifficulty)
                ? slotDifficulty
                : _random.Pick(availableDifficulties);
            // Sunrise added end

            var mission = new SalvageMissionParams
            {
                Index = component.NextIndex,
                Seed = _random.Next(),
                Difficulty = difficulty, // Sunrise-Edit
            };

            component.Missions[component.NextIndex++] = mission;
        }
    }

    private SalvageExpeditionConsoleState GetState(SalvageExpeditionDataComponent component)
    {
        var missions = component.Missions.Values.ToList();
        return new SalvageExpeditionConsoleState(component.NextOffer, component.Claimed, component.Cooldown, component.ActiveMission, missions);
    }

    private void SpawnMission(SalvageMissionParams missionParams, EntityUid station, EntityUid? coordinatesDisk)
    {
        var cancelToken = new CancellationTokenSource();
        var job = new SpawnSalvageMissionJob(
            SalvageJobTime,
            EntityManager,
            _timing,
            LogManager,
            ProtoMan,
            _anchorable,
            _biome,
            _dungeon,
            _metaData,
            _mapSystem,
            station,
            coordinatesDisk,
            missionParams,
            cancelToken.Token);

        _salvageJobs.Add((job, cancelToken));
        _salvageQueue.EnqueueJob(job);
    }
}

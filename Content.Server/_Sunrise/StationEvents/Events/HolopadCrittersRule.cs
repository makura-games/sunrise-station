using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Content.Shared.Storage;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Content.Shared.Holopad;

namespace Content.Server.StationEvents.Events;

/// <summary>
/// Station event that spawns critters from holopads.
/// Similar to VentCrittersRule but for holopads.
/// </summary>
public sealed class HolopadCrittersRule : StationEventSystem<HolopadCrittersRuleComponent>
{
    protected override void Started(EntityUid uid, HolopadCrittersRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station))
        {
            Log.Warning($"Unable to find a valid station for holopad critters event!");
            return;
        }

        // Get all holopads with spawn location component
        var holopadQuery = EntityQueryEnumerator<HolopadComponent, TransformComponent>();
        var validHolopads = new List<EntityCoordinates>();

        while (holopadQuery.MoveNext(out _, out _, out var transform))
        {
            // Must be anchored
            if (!transform.Anchored)
                continue;

            // Must be on the same station
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station == station)
            {
                validHolopads.Add(transform.Coordinates);
            }
        }

        if (validHolopads.Count == 0)
        {
            Log.Warning($"No valid holopads found for holopad critters event on station {station}!");
            return;
        }

        if (component.SpecialEntries.Count > 0)
        {
            var specialSpawns = EntitySpawnCollection.GetSpawns(component.SpecialEntries, RobustRandom);

            // Determine which holopads to use
            List<EntityCoordinates> targetHolopads;

            if (component.SpawnFromAllHolopads)
            {
                targetHolopads = validHolopads;
            }
            else
            {
                var count = Math.Min(component.MaxHolopadsToSpawn, validHolopads.Count);
                targetHolopads = new List<EntityCoordinates>();

                for (int i = 0; i < count; i++)
                {
                    var holopad = RobustRandom.PickAndTake(validHolopads);
                    targetHolopads.Add(holopad);
                }
            }

            foreach (var holopad in targetHolopads)
            {
                foreach (var spawn in specialSpawns)
                {
                    Spawn(spawn, holopad);
                }
            }
        }

        // Spawn regular entries on all holopads (if configured)
        if (component.Entries.Count > 0 && component.SpawnFromAllHolopads)
        {
            var regularSpawns = EntitySpawnCollection.GetSpawns(component.Entries, RobustRandom);

            foreach (var holopad in validHolopads)
            {
                foreach (var spawn in regularSpawns)
                {
                    Spawn(spawn, holopad);
                }
            }
        }

        // Disable holopads after spawning (optional)
        if (component.DisableHolopadsAfterSpawn)
        {
            var holopads = EntityQueryEnumerator<HolopadComponent>();
            while (holopads.MoveNext(out var holopadUid, out _))
            {
                RemComp<HolopadComponent>(holopadUid);
            }
        }

        Log.Info($"Holopad critters event started: {validHolopads.Count} holopads, {component.Entries.Count + component.SpecialEntries.Count} spawn entries.");
    }
}
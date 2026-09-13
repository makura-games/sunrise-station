using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._Sunrise.Shuttles.Components;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Sunrise.Shuttles;

[TestFixture]
public sealed class ConcurrentArrivalsTest
{
    private const int LandingTickBudget = 600;
    private const int TicksPerPoll = 5;

    [Test]
    [Description("prevents a second arrivals shuttle from targeting a different dock when its landing area overlaps the first shuttle")]
    public async Task ReservedDockDoesNotAllowOverlappingArrivalArea()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var docking = server.System<DockingSystem>();
        var map = await pair.CreateTestMap();

        DockingConfig firstConfig = null!;
        DockingConfig secondConfig = null!;
        var hasFirstConfig = false;
        var hasSecondConfig = false;

        await server.WaitPost(() =>
        {
            entMan.DeleteEntity(map.Grid);

            var station = MakeStation(pair, map.MapId, 8, 2, 5);
            var first = MakeShuttle(pair, map.MapId, new Vector2(-100f, 0f));
            var second = MakeShuttle(pair, map.MapId, new Vector2(-200f, 0f));

            firstConfig = docking.GetDockingConfig(first, station);
            if (firstConfig == null)
                return;

            hasFirstConfig = true;
            Reserve(entMan, first, firstConfig);

            secondConfig = docking.GetDockingConfig(second, station);
            hasSecondConfig = secondConfig != null;
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(hasFirstConfig, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(hasSecondConfig && SharesTargetDock(firstConfig, secondConfig), Is.False,
                    "the second shuttle reused a dock reserved by the first shuttle");
                Assert.That(hasSecondConfig && HasOverlappingArea(firstConfig, secondConfig), Is.False,
                    "the second shuttle was dispatched into an area reserved by the first shuttle");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    [Description("ftls two arrivals shuttles to non-overlapping docks in the same tick and keeps both passengers alive")]
    public async Task NonOverlappingArrivalsCanFtlConcurrently()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var docking = server.System<DockingSystem>();
        var shuttleSystem = server.System<ShuttleSystem>();
        var map = await pair.CreateTestMap();

        var first = EntityUid.Invalid;
        var second = EntityUid.Invalid;
        var firstDock = EntityUid.Invalid;
        var secondDock = EntityUid.Invalid;
        var firstPassenger = EntityUid.Invalid;
        var secondPassenger = EntityUid.Invalid;
        DockingConfig firstConfig = null!;
        DockingConfig secondConfig = null!;
        var hasFirstConfig = false;
        var hasSecondConfig = false;

        await server.WaitPost(() =>
        {
            entMan.DeleteEntity(map.Grid);

            var station = MakeStation(pair, map.MapId, 14, 2, 10);
            first = MakeShuttle(pair, map.MapId, new Vector2(-100f, 0f));
            second = MakeShuttle(pair, map.MapId, new Vector2(-200f, 0f));
            firstDock = GetShuttleDock(entMan, first);
            secondDock = GetShuttleDock(entMan, second);
            firstPassenger = entMan.SpawnEntity("MobHuman", new EntityCoordinates(first, 0.5f, 1.5f));
            secondPassenger = entMan.SpawnEntity("MobHuman", new EntityCoordinates(second, 0.5f, 1.5f));

            entMan.AddComponent<SunriseArrivalsShuttleComponent>(first);
            entMan.AddComponent<SunriseArrivalsShuttleComponent>(second);

            firstConfig = docking.GetDockingConfig(first, station);
            if (firstConfig == null)
                return;

            hasFirstConfig = true;
            Reserve(entMan, first, firstConfig);

            secondConfig = docking.GetDockingConfig(second, station);
            if (secondConfig == null)
                return;

            hasSecondConfig = true;
            Reserve(entMan, second, secondConfig);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(hasFirstConfig, Is.True);
            Assert.That(hasSecondConfig, Is.True);
            Assert.That(HasOverlappingArea(firstConfig, secondConfig), Is.False);
        });

        await server.WaitPost(() =>
        {
            shuttleSystem.FTLToCoordinates(first,
                entMan.GetComponent<ShuttleComponent>(first),
                firstConfig.Coordinates,
                firstConfig.Angle,
                startupTime: 0f,
                hyperspaceTime: 0f);
            shuttleSystem.FTLToCoordinates(second,
                entMan.GetComponent<ShuttleComponent>(second),
                secondConfig.Coordinates,
                secondConfig.Angle,
                startupTime: 0f,
                hyperspaceTime: 0f);
        });

        await WaitForLanding(pair, first, second);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.Deleted(first), Is.False);
                Assert.That(entMan.Deleted(second), Is.False);
                Assert.That(entMan.Deleted(firstPassenger), Is.False);
                Assert.That(entMan.Deleted(secondPassenger), Is.False);
                Assert.That(entMan.GetComponent<DockingComponent>(firstDock).Docked, Is.True);
                Assert.That(entMan.GetComponent<DockingComponent>(secondDock).Docked, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void Reserve(IEntityManager entMan, EntityUid shuttle, DockingConfig config)
    {
        foreach (var (_, dock, _, _) in config.Docks)
        {
            var reservation = entMan.EnsureComponent<FtlReservationComponent>(dock);
            reservation.ReservedBy = shuttle;
            reservation.Area = config.Area;
        }
    }

    private static bool SharesTargetDock(DockingConfig first, DockingConfig second)
    {
        foreach (var firstDock in first.Docks)
        {
            foreach (var secondDock in second.Docks)
            {
                if (firstDock.DockBUid == secondDock.DockBUid)
                    return true;
            }
        }

        return false;
    }

    private static bool HasOverlappingArea(DockingConfig first, DockingConfig second)
    {
        return Box2.Area(first.Area.Intersect(second.Area)) > 0f;
    }

    private static EntityUid MakeStation(TestPair pair, MapId mapId, int width, params int[] docks)
    {
        var server = pair.Server;
        var map = server.System<SharedMapSystem>();
        var station = server.MapMan.CreateGridEntity(mapId);

        // - is tile; + is dock
        // --+--+-- width 8 docks 2, 5
        // --+-------+--- width 14 docks 2, 10.
        for (var x = 0; x < width; x++)
        {
            map.SetTile(station.Owner, station.Comp, new Vector2i(x, 0), new Tile(1));
        }

        foreach (var dock in docks)
        {
            server.EntMan.SpawnEntity("AirlockShuttle", new EntityCoordinates(station.Owner, dock, 0f));
        }

        return station.Owner;
    }

    private static EntityUid MakeShuttle(TestPair pair, MapId mapId, Vector2 position)
    {
        var server = pair.Server;
        var map = server.System<SharedMapSystem>();
        var transform = server.System<SharedTransformSystem>();
        var shuttle = server.MapMan.CreateGridEntity(mapId);

        // shuttle preview
        // #####
        // #####
        // ##+##
        for (var x = -2; x <= 2; x++)
        {
            for (var y = 0; y < 3; y++)
            {
                map.SetTile(shuttle.Owner, shuttle.Comp, new Vector2i(x, y), new Tile(1));
            }
        }

        transform.SetLocalPosition(shuttle.Owner, position);
        server.EntMan.SpawnEntity("AirlockShuttle", new EntityCoordinates(shuttle.Owner, 0f, 0f));
        return shuttle.Owner;
    }

    private static EntityUid GetShuttleDock(IEntityManager entMan, EntityUid shuttle)
    {
        var query = entMan.EntityQueryEnumerator<DockingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.GridUid == shuttle)
                return uid;
        }

        return EntityUid.Invalid;
    }

    private static async Task WaitForLanding(TestPair pair, params EntityUid[] shuttles)
    {
        var entMan = pair.Server.EntMan;

        for (var i = 0; i < LandingTickBudget; i += TicksPerPoll)
        {
            await pair.Server.WaitRunTicks(TicksPerPoll);

            var landed = true;
            foreach (var shuttle in shuttles)
            {
                if (entMan.TryGetComponent<FTLComponent>(shuttle, out var ftl) &&
                    ftl.State is not (FTLState.Cooldown or FTLState.Available))
                {
                    landed = false;
                    break;
                }
            }

            if (landed)
                return;
        }

        Assert.Fail("shuttles never finished FTL");
    }
}

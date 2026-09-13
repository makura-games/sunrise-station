#nullable enable
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
public sealed class ArrivalsTest
{
    private const string MobProto = "MobHuman";
    private const int LandingTickBudget = 600;
    private const int TicksPerPoll = 5;

    private static readonly Vector2 Landing = new(50f, 50f);

    private static readonly Vector2i[] StationTiles =
    {
        new(0, 0), new(0, 1), new(0, 2), new(-1, 2), new(1, 2),
    };

    private static readonly Vector2i[] ShuttleTiles =
    {
        new(0, 0), new(0, 1), new(0, 2),
    };

    [TestCase(true)]
    [TestCase(false)]
    [Description("drops a shuttle on a mob standing on the map nd it gets gibbed unless it has ftl smash immunity")]
    public async Task MobGibbedUnlessImmune(bool immune)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        var mob = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            mob = entMan.SpawnEntity(MobProto, new MapCoordinates(Landing, map.MapId));
            if (immune)
                entMan.EnsureComponent<FTLSmashImmuneComponent>(mob);
        });

        await FtlOnto(pair, map.MapId, new EntityCoordinates(map.MapUid, Landing));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(mob), Is.EqualTo(!immune));
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    [Description("drops a shuttle on a mob riding another grid, smash only reaches stuff parented to the map so it lives")]
    public async Task MobOnOtherShuttleSurvives(bool immune)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        var mob = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var other = server.MapMan.CreateGridEntity(map.MapId);
            mapSys.SetTile(other.Owner, other.Comp, Vector2i.Zero, new Tile(1));
            xformSys.SetLocalPosition(other.Owner, Landing);

            mob = entMan.SpawnEntity(MobProto, new EntityCoordinates(other.Owner, 0.5f, 0.5f));
            if (immune)
                entMan.EnsureComponent<FTLSmashImmuneComponent>(mob);
        });

        await FtlOnto(pair, map.MapId, new EntityCoordinates(map.MapUid, Landing));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(mob), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    [Description("ftls an arrivals shuttle into a dock thats already taken or reserved")]
    public async Task ArrivalsShuttleSkipsTakenDock(bool docked)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var dockSys = server.System<DockingSystem>();
        var shuttleSys = server.System<ShuttleSystem>();
        var map = await pair.CreateTestMap();

        var secondUid = EntityUid.Invalid;
        var secondDock = EntityUid.Invalid;
        DockingConfig? firstConfig = null;
        DockingConfig? secondConfig = null;

        await server.WaitPost(() =>
        {
            entMan.DeleteEntity(map.Grid);

            var (station, _) = MakeGrid(pair, map.MapId, new Vector2(100f, 100f), StationTiles);
            var (first, _) = MakeGrid(pair, map.MapId, Vector2.Zero, ShuttleTiles);
            var (second, secondDockUid) = MakeGrid(pair, map.MapId, new Vector2(50f, 0f), ShuttleTiles);

            secondUid = second;
            secondDock = secondDockUid;

            var arrivals = entMan.AddComponent<SunriseArrivalsShuttleComponent>(second);
            arrivals.SpawnTime = server.Timing.CurTime;

            firstConfig = dockSys.GetDockingConfig(first, station);
            secondConfig = dockSys.GetDockingConfig(second, station);

            if (firstConfig == null || secondConfig == null)
                return;

            if (docked)
            {
                shuttleSys.FTLDock((first, entMan.GetComponent<TransformComponent>(first)), firstConfig);
            }
            else
            {
                foreach (var (_, dockBUid, _, _) in firstConfig.Docks)
                {
                    entMan.EnsureComponent<FtlReservationComponent>(dockBUid).ReservedBy = first;
                }
            }

            var shuttle = entMan.GetComponent<ShuttleComponent>(second);
            shuttleSys.FTLToCoordinates(second, shuttle, secondConfig.Coordinates, secondConfig.Angle,
                startupTime: 0f, hyperspaceTime: 0f);
        });

        Assert.Multiple(() =>
        {
            Assert.That(firstConfig, Is.Not.Null);
            Assert.That(secondConfig, Is.Not.Null);
        });

        await WaitForLanding(pair, secondUid);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DockingComponent>(secondDock).Docked, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    private static async Task FtlOnto(TestPair pair, MapId mapId, EntityCoordinates target)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var shuttleSys = server.System<ShuttleSystem>();

        var shuttleUid = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var grid = server.MapMan.CreateGridEntity(mapId);
            shuttleUid = grid.Owner;

            for (var x = -2; x <= 2; x++)
            {
                for (var y = -2; y <= 2; y++)
                {
                    mapSys.SetTile(shuttleUid, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            xformSys.SetLocalPosition(shuttleUid, new Vector2(-200f, -200f));

            var shuttle = entMan.GetComponent<ShuttleComponent>(shuttleUid);
            shuttleSys.FTLToCoordinates(shuttleUid, shuttle, target, Angle.Zero, startupTime: 0f, hyperspaceTime: 0f);
        });

        await WaitForLanding(pair, shuttleUid);
    }

    private static (EntityUid Grid, EntityUid Dock) MakeGrid(TestPair pair, MapId mapId, Vector2 offset, Vector2i[] tiles)
    {
        var server = pair.Server;
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();

        var grid = server.MapMan.CreateGridEntity(mapId);
        foreach (var tile in tiles)
        {
            mapSys.SetTile(grid.Owner, grid.Comp, tile, new Tile(1));
        }

        xformSys.SetLocalPosition(grid.Owner, offset);
        var dock = server.EntMan.SpawnEntity("AirlockShuttle", new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));

        return (grid.Owner, dock);
    }

    private static async Task WaitForLanding(TestPair pair, EntityUid shuttle)
    {
        var entMan = pair.Server.EntMan;

        for (var i = 0; i < LandingTickBudget; i += TicksPerPoll)
        {
            await pair.Server.WaitRunTicks(TicksPerPoll);

            if (!entMan.TryGetComponent<FTLComponent>(shuttle, out var ftl) || ftl.State == FTLState.Cooldown)
                return;
        }

        Assert.Fail($"shuttle {shuttle} never finished ftl");
    }
}

using Content.Server.GameTicking;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Sunrise;

[TestFixture]
public sealed class GameMapSourceTest
{
    private static readonly ProtoId<GameMapPrototype> EmptyMap = "Empty";

    [Test]
    public async Task ContentOnlyGameMapIgnoresUserDataOverride()
    {
        var mapPath = new ResPath("/Maps/Test/empty.yml");

        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });

        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var resources = server.ResolveDependency<IResourceManager>();
        var ticker = server.EntMan.System<GameTicker>();
        var mapSystem = server.EntMan.System<SharedMapSystem>();
        MapId loadedMapId = default;

        await server.WaitPost(() =>
        {
            cfg.SetCVar(SunriseCCVars.GameMapUseUserData, false);
            resources.UserData.CreateDir(mapPath.Directory);
            using var writer = resources.UserData.OpenWriteText(mapPath);
            writer.Write("invalid map override");
        });

        try
        {
            await server.WaitPost(() =>
            {
                var map = prototypes.Index(EmptyMap);
                var options = DeserializationOptions.Default with { InitializeMaps = false };
                ticker.LoadGameMap(map, out loadedMapId, options);
            });

            await server.WaitAssertion(() =>
            {
                Assert.That(mapSystem.MapExists(loadedMapId), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                resources.UserData.Delete(mapPath);
                cfg.SetCVar(SunriseCCVars.GameMapUseUserData, true);
            });
        }

        await pair.CleanReturnAsync();
    }
}

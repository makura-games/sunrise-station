using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.IntegrationTests.Tests.Atmos;

/// <summary>
/// Checks networking of fire visuals inside GasTileOverlay
/// </summary>
public sealed partial class SharedGasTileOverlayTest
{
    [Test]
    [Description("Checks networking of fire visuals inside GasTileOverlay.")]
    public async Task TestGasTileFireOverlayDataSync()
    {
        var (gridCoords, tileIndices, mixture, cOverlay) = await PrepareGasTileTest();

        await Server.WaitAssertion(() =>
        {
            Assert.That(mixture, Is.Not.Null, "The gas mixture was not initialized.");
        });

        var activated = false;
        await Server.WaitPost(() =>
        {
            mixture.Clear();
            mixture.AdjustMoles(Gas.Plasma, 100f);
            mixture.AdjustMoles(Gas.Oxygen, 100f); // Inject flamable gasses

            var welder = SSpawnAtPosition("Welder", gridCoords);
            activated = _itemToggle.TryActivate(welder);
        });
        await Server.WaitAssertion(() => Assert.That(activated));

        await RunUntilSynced();

        var fireStateSynced = false;
        var syncTimeoutTicks = Pair.SecondsToTicks(1f);
        for (var i = 0; i < syncTimeoutTicks && !fireStateSynced; i++)
        {
            await Pair.RunTicksSync(1);
            await Client.WaitPost(() =>
            {
                fireStateSynced = TryGetFireState(cOverlay, tileIndices, out var fireState) && fireState > 0;
            });
        }

        await Client.WaitAssertion(() =>
        {
            Assert.That(TryGetFireState(cOverlay, tileIndices, out var fireState), Is.True, "Chunk not found");
            Assert.That(fireState, Is.GreaterThan(0), $"Tile at {tileIndices} is not set on fire!");
        });
    }

    private static bool TryGetFireState(GasTileOverlayComponent overlay, Vector2i tileIndices, out byte fireState)
    {
        fireState = 0;
        var chunkIndices = SharedGasTileOverlaySystem.GetGasChunkIndices(tileIndices);
        if (!overlay.Chunks.TryGetValue(chunkIndices, out var chunk) || chunk == null)
            return false;

        var localX = MathHelper.Mod(tileIndices.X, SharedGasTileOverlaySystem.ChunkSize);
        var localY = MathHelper.Mod(tileIndices.Y, SharedGasTileOverlaySystem.ChunkSize);
        var tileIndex = localX + localY * SharedGasTileOverlaySystem.ChunkSize;

        fireState = chunk.TileData[tileIndex].FireState;
        return true;
    }

    private async Task<(EntityCoordinates gridCoords, Vector2i tileIndices, GasMixture mixture, GasTileOverlayComponent cOverlay)> PrepareGasTileTest()
    {
        await Server.WaitPost(delegate
        {
            // funny thing, this grid is a star so we need to spawn some ents to give us one cell
            // otherwise the gas will spread to other areas and itll be weird
            for (var i = 0; i < Atmospherics.Directions; i++)
            {
                var direction = (AtmosDirection)(1 << i);
                var offsetOrigin = Vector2i.Zero.Offset(direction);
                SSpawnAtPosition("WallSolid", new EntityCoordinates(ProcessEnt, offsetOrigin));
            }
        });

        await RunUntilSynced();
        var gridComp = ProcessEnt.Comp3;
        var gridNetEnt = Server.EntMan.GetNetEntity(ProcessEnt);
        var gridCoords = new EntityCoordinates(ProcessEnt, Vector2.Zero);
        var tileIndices = _mapSys.TileIndicesFor(ProcessEnt, gridComp, gridCoords);
        var mixture = SAtmos.GetTileMixture(ProcessEnt, null, tileIndices, true);

        // Get data for client side.
        var cGridEnt = CEntMan.GetEntity(gridNetEnt);
        Assert.That(CTryComp<GasTileOverlayComponent>(cGridEnt, out var cOverlay),
            "Client grid is missing GasTileOverlayComponent");

        // Check if the server actually sent the gas chunks
        await RunUntilSynced();
        Assert.That(cOverlay, Is.Not.Null, "Gas overlay is null on the client.");
        Assert.That(cOverlay.Chunks, Is.Not.Empty, "Gas overlay chunks are empty on the client.");
        return (gridCoords, tileIndices, mixture, cOverlay);
    }
}

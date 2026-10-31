#nullable enable
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Holosign;
using Content.Shared.PowerCell;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests.Holosign;

/// <summary>
/// Tests for different devices using <see cref="HolosignProjectorComponent"/>.
/// </summary>
[TestOf(typeof(HolosignProjectorComponent))]
public sealed class HolosignProjectorTest : MovementTest
{
    private static readonly EntProtoId HoloBarrierProjectorProtoId = "HoloprojectorSecurity";
    private static readonly EntProtoId HoloSignProjectorProtoId = "Holoprojector";

    /// <summary>
    /// Tests the janitors holosign projector.
    /// </summary>
    [Test]
    public async Task HoloSignTest()
    {
        var projector = await PlaceInHands(HoloSignProjectorProtoId);
        var projectorComp = Comp<HolosignProjectorComponent>(projector);
        var signProtoId = projectorComp.SignProto;

        // No holosigns before using the item.
        await AssertEntityLookup((WallPrototype, 2));

        var powerCellSystem = SEntMan.System<PowerCellSystem>();
        var initialUses = powerCellSystem.GetRemainingUses(ToServer(projector), projectorComp.ChargeUse);
        Assert.That(initialUses, Is.GreaterThan(0), "Holoprojector spawned without usable charges.");

        // Click on the tile next to the player.
        await Interact(null, TargetCoords);

        // We should have one charge less.
        var remainingUses = powerCellSystem.GetRemainingUses(ToServer(projector), projectorComp.ChargeUse);
        Assert.That(remainingUses, Is.EqualTo(initialUses - 1), "Holoprojector did not use the right amount of charge when used.");

        // We should have spawned exactly one holosign.
        await AssertEntityLookup(
            (signProtoId, 1),
            (WallPrototype, 2));

        // Try spawn more holosigns than we have charge.
        for (var i = 0; i < initialUses; i++)
        {
            await Interact(null, TargetCoords);
        }

        // Sunrise edit start - учитываем ограничение числа одинаковых голограмм на тайле
        var expectedSignCount = Math.Min(initialUses, projectorComp.CountPerTileLimit);

        // Общее число голограмм не должно превышать ограничение на тайле.
        await AssertEntityLookup(
            (signProtoId, expectedSignCount),
            (WallPrototype, 2));

        // Отклонённые из-за ограничения попытки не должны расходовать заряд.
        remainingUses = powerCellSystem.GetRemainingUses(ToServer(projector), projectorComp.ChargeUse);
        Assert.That(remainingUses,
            Is.EqualTo(initialUses - expectedSignCount),
            "Holoprojector used charge for a holosign blocked by the per-tile limit.");
        // Sunrise edit end
    }

    /// <summary>
    /// Tests the security holo barrier projector and the barrier.
    /// </summary>
    [Test]
    public async Task HoloBarrierTest()
    {
        var projector = await PlaceInHands(HoloBarrierProjectorProtoId);
        var holoBarrierProtoId = Comp<HolosignProjectorComponent>(projector).SignProto;
        // No holobarriers before using the item.
        await AssertEntityLookup((WallPrototype, 2));

        // Click on the tile next to the player.
        await Interact(null, TargetCoords);

        // We should have spawned exactly one holobarrier.
        await AssertEntityLookup(
            (holoBarrierProtoId, 1),
            (WallPrototype, 2));
        Target = FromServer(await FindEntity(holoBarrierProtoId));
        var timeRemaining = Comp<TimedDespawnComponent>(Target).Lifetime;

        // Check that the barrier is at the location we clicked at.
        AssertLocation(Target, TargetCoords);

        // Try moving past the barrier.
        Assert.That(Delta(), Is.GreaterThan(0.5), "Player was not located west of the holobarrier.");
        await Move(DirectionFlag.East, 0.5f);
        Assert.That(Delta(), Is.GreaterThan(0.5), "Player was able to walk through a holobarrier.");

        // Try to climb the barrier.
        await Interact(Target, TargetCoords, altInteract: true);

        // We should be able to move past the barrier now.
        await Move(DirectionFlag.East, 0.5f);
        Assert.That(Delta(), Is.LessThan(-0.5), "Player was not able to climb over a holobarrier.");

        // We should not be able to walk back without climbing again.
        await Move(DirectionFlag.West, 0.5f);
        Assert.That(Delta(), Is.LessThan(-0.5), "Player was able to walk through a holobarrier.");

        // Wait until the barrier despawns.
        await RunSeconds(timeRemaining);
        AssertDeleted(Target);

        // We should be able to walk back now.
        await Move(DirectionFlag.West, 0.5f);
        Assert.That(DeltaCoordinates(), Is.GreaterThan(0.5), "Player was able to walk past a deleted holobarrier.");
    }
}

using System.Collections.Generic;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests.Tests.Humanoid;

[TestOf(typeof(SharedHideableHumanoidLayersSystem))]
public sealed class HideableHumanoidLayersTest : InteractionTest
{
    protected override string PlayerPrototype => "MobVulpkanin";

    // Sunrise edit start - Sunrise заменяет стандартные маркировки морды Vulpkanin своим набором.
    private const string SnoutLayerId = "VulpSnout-muzzle";
    private const string SnoutCoverMarkingId = "VoxBeakCoverStripe";
    private const string SnoutCoverLayerId = "VoxBeakCoverStripe-beakcover_stripe";
    // Sunrise edit end

    [Test]
    public async Task BasicHiding()
    {
        await SpawnTarget("ClothingMaskGas");
        await Pickup(); // equip mask
        await UseInHand();

        await Server.WaitAssertion(() =>
        {
            var hideableHumanoidLayers = SEntMan.GetComponent<HideableHumanoidLayersComponent>(SPlayer);
            Assert.That(hideableHumanoidLayers.HiddenLayers, Does.ContainKey(HumanoidVisualLayers.Snout).WithValue(SlotFlags.MASK));
        });

        await Server.WaitAssertion(() =>
        {
            SEntMan.DeleteEntity(STarget); // de-equip mask

            var hideableHumanoidLayers = SEntMan.GetComponent<HideableHumanoidLayersComponent>(SPlayer);
            Assert.That(hideableHumanoidLayers.HiddenLayers, Does.Not.ContainKey(HumanoidVisualLayers.Snout));
        });
    }

    [Test]
    public async Task DependentHiding()
    {
        await Server.WaitAssertion(() =>
        {
            var visualBody = SEntMan.System<SharedVisualBodySystem>();
            visualBody.ApplyMarkings(SPlayer, new()
            {
                ["Head"] = new()
                {
                    [HumanoidVisualLayers.SnoutCover] = new List<Marking>() { new(SnoutCoverMarkingId, 1) }, // Sunrise-Edit
                },
            });
        });

        await SpawnTarget("ClothingMaskGas");
        await Pickup(); // equip mask
        await UseInHand();

        await RunTicks(20);

        await Client.WaitAssertion(() =>
        {
            var spriteSystem = CEntMan.System<SpriteSystem>();
            var snoutIndex = spriteSystem.LayerMapGet(CPlayer, SnoutLayerId); // Sunrise-Edit
            var snoutCoverIndex = spriteSystem.LayerMapGet(CPlayer, SnoutCoverLayerId); // Sunrise-Edit
            var spriteComp = CEntMan.GetComponent<SpriteComponent>(CPlayer);

            Assert.That(spriteComp[snoutIndex].Visible, Is.False);
            Assert.That(spriteComp[snoutCoverIndex].Visible, Is.False);
        });

        await Server.WaitAssertion(() =>
        {
            SEntMan.DeleteEntity(STarget); // de-equip mask
        });

        await RunTicks(20);

        await Client.WaitAssertion(() =>
        {
            var spriteSystem = CEntMan.System<SpriteSystem>();
            var snoutIndex = spriteSystem.LayerMapGet(CPlayer, SnoutLayerId); // Sunrise-Edit
            var snoutCoverIndex = spriteSystem.LayerMapGet(CPlayer, SnoutCoverLayerId); // Sunrise-Edit
            var spriteComp = CEntMan.GetComponent<SpriteComponent>(CPlayer);

            Assert.That(spriteComp[snoutIndex].Visible, Is.True);
            Assert.That(spriteComp[snoutCoverIndex].Visible, Is.True);
        });
    }
}

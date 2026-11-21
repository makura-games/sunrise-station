using Content.IntegrationTests.Fixtures;
using Content.Server._Sunrise.SponsorInventory;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Sunrise.SponsorInventory;

/// <summary>
/// Проверяет выдачу спонсорской вещи при появлении: она заменяет штатную вещь в занятом слоте,
/// а ID-карта, вещи из карманов и содержимое рюкзака не теряются.
/// </summary>
[TestFixture, NonParallelizable]
public sealed class SponsorInventoryEquipTests : GameTest
{
    private const string ReplacementJumpsuitId = "SunriseTestSponsorReplacementJumpsuit";

    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: SunriseTestSponsorReplacementJumpsuit
          parent: ClothingUniformJumpsuitColorGrey
        """;

    [Test]
    public async Task SponsorItemReplacesOccupiedSlotAndKeepsContents()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var storage = SEntMan.System<SharedStorageSystem>();
            var sponsorInventory = SEntMan.System<SunriseInventorySystem>();

            var human = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var oldJumpsuit = SEntMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
            var idCard = SEntMan.SpawnEntity("PassengerIDCard", map.GridCoords);
            var pocketItem = SEntMan.SpawnEntity("Pen", map.GridCoords);
            var oldBackpack = SEntMan.SpawnEntity("ClothingBackpack", map.GridCoords);
            var bagItem = SEntMan.SpawnEntity("Pen", map.GridCoords);

            // Штатная экипировка профессии, как после выдачи лодаута.
            Assert.That(inventory.TryEquip(human, oldJumpsuit, "jumpsuit", silent: true, force: true));
            Assert.That(inventory.TryEquip(human, idCard, "id", silent: true, force: true));
            Assert.That(inventory.TryEquip(human, pocketItem, "pocket1", silent: true, force: true));
            Assert.That(inventory.TryEquip(human, oldBackpack, "back", silent: true, force: true));
            Assert.That(storage.Insert(oldBackpack, bagItem, out _, playSound: false));

            var jumpsuit = new SponsorInventoryItemInfo { Id = "test-jumpsuit", EntityPrototype = ReplacementJumpsuitId };
            Assert.That(sponsorInventory.TryEquipSponsorItem(human, "jumpsuit", jumpsuit),
                "Спонсорская форма не заменила штатную в занятом слоте.");
            Assert.That(inventory.TryGetSlotEntity(human, "jumpsuit", out var newJumpsuit));
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(newJumpsuit!.Value).EntityPrototype?.ID,
                Is.EqualTo(ReplacementJumpsuitId));
            Assert.That(inventory.TryGetSlotEntity(human, "id", out var idAfter) && idAfter == idCard,
                "ID-карта не вернулась в слот после замены формы.");
            Assert.That(inventory.TryGetSlotEntity(human, "pocket1", out var pocketAfter) && pocketAfter == pocketItem,
                "Вещь из кармана не вернулась после замены формы.");
            Assert.That(SEntMan.Deleted(oldJumpsuit) || SEntMan.IsQueuedForDeletion(oldJumpsuit),
                "Штатная форма не удалена после замены.");

            var satchel = new SponsorInventoryItemInfo { Id = "test-satchel", EntityPrototype = "ClothingBackpackSatchel" };
            Assert.That(sponsorInventory.TryEquipSponsorItem(human, "back", satchel),
                "Спонсорская сумка не заменила штатный рюкзак.");
            Assert.That(inventory.TryGetSlotEntity(human, "back", out var newBack));
            Assert.That(SEntMan.GetComponent<StorageComponent>(newBack!.Value).Container.ContainedEntities,
                Does.Contain(bagItem), "Содержимое рюкзака не перенесено в новую сумку.");
        });
    }
}

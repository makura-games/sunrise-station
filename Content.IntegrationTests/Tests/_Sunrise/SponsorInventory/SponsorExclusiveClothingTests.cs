using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.SponsorInventory;

/// <summary>
/// Проверяет, что каждую эксклюзивную спонсорскую одежду можно выдать человеку:
/// вещь помещается хотя бы в один слот, а присоединённые части (шлемы, капюшоны, звенья МОДа) создаются вместе с ней.
/// </summary>
[TestFixture, NonParallelizable]
public sealed class SponsorExclusiveClothingTests : GameTest
{
    private const string CategoryId = "SponsorExclusive";
    private const string HumanPrototype = "MobHuman";
    private const string TestPrototypePrefix = "SunriseTest";

    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task ExclusiveClothingFitsHumanAndSpawnsParts()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var category = SProtoMan.Index<EntityCategoryPrototype>(CategoryId);
            // Проверяем только одежду: эксклюзивом в будущем может стать и не надеваемая вещь.
            var items = SProtoMan.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => !proto.Abstract
                                && proto.Categories.Contains(category)
                                && proto.Components.ContainsKey("Clothing")
                                && !proto.ID.StartsWith(TestPrototypePrefix))
                .OrderBy(proto => proto.ID)
                .ToList();

            var human = SEntMan.SpawnEntity(HumanPrototype, map.GridCoords);
            Assert.That(inventory.TryGetSlots(human, out var slots), "У человека нет слотов инвентаря.");

            Assert.Multiple(() =>
            {
                foreach (var proto in items)
                {
                    var item = SEntMan.SpawnEntity(proto.ID, map.GridCoords);
                    Assert.That(SEntMan.HasComponent<ClothingComponent>(item), $"{proto.ID}: нет компонента Clothing.");

                    // Сервер выдаёт покупку в выбранный слот вместо штатной вещи, поэтому слот считаем пустым.
                    var fits = slots!.Any(slot => inventory.CanEquip(human, item, slot.Name, out _, assumeEmpty: true));
                    Assert.That(fits, $"{proto.ID}: не помещается ни в один слот человека.");

                    foreach (var part in AttachedParts(item))
                    {
                        Assert.That(SEntMan.HasComponent<ClothingComponent>(part),
                            $"{proto.ID}: у части {SEntMan.GetComponent<MetaDataComponent>(part).EntityPrototype?.ID} нет Clothing.");
                    }

                    SEntMan.DeleteEntity(item);
                }
            });

            SEntMan.DeleteEntity(human);
        });
    }

    /// <summary>
    /// Обходит цепочку присоединённых частей: шлем скафандра, капюшон или следующее звено МОДа.
    /// </summary>
    private IEnumerable<EntityUid> AttachedParts(EntityUid item)
    {
        var current = item;
        var visited = new HashSet<EntityUid> { item };
        while (SEntMan.TryGetComponent<ToggleableClothingComponent>(current, out var toggleable))
        {
            Assert.That(toggleable.ClothingUid, Is.Not.Null,
                $"{SEntMan.GetComponent<MetaDataComponent>(current).EntityPrototype?.ID}: присоединённая часть не создана.");
            if (toggleable.ClothingUid is not { } part || !visited.Add(part))
                yield break;

            yield return part;
            current = part;
        }
    }
}

using System.Linq;
using Content.Shared._Sunrise.VendingMachines;
using Content.Shared.VendingMachines.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

#pragma warning disable IDE0130
namespace Content.Shared.VendingMachines;

public abstract partial class SharedVendingMachineSystem
{
    [Dependency] private ISharedPlayerManager _player = default!;

    /// <summary>
    /// Добавляет один случайный товар из стартового запаса автомата либо заданный прототип.
    /// </summary>
    public void RestockRandom(Entity<VendingMachineComponent> ent)
    {
        string item;

        if (ent.Comp.RandomRestockTarget is { } forcedItem)
        {
            item = forcedItem;
        }
        else
        {
            if (!ProtoMan.TryIndex(ent.Comp.PackPrototypeId, out VendingMachineInventoryPrototype? packPrototype) ||
                packPrototype.StartingInventory.Count == 0)
            {
                return;
            }

            var index = Randomizer.Next(packPrototype.StartingInventory.Count);
            item = packPrototype.StartingInventory.ElementAt(index).Key;
        }

        AddInventoryFromPrototype(ent, new Dictionary<EntProtoId, uint> { [item] = 1 }, InventoryType.Regular, ent.Comp);
        Dirty(ent);
    }

    private uint AdjustSunriseRestock(EntityUid uid,
        VendingMachineComponent component,
        InventoryType type,
        uint amount,
        uint restock)
    {
        if (type is InventoryType.Regular or InventoryType.Contraband)
        {
            if (TryComp<PlayerCountDependentStockComponent>(uid, out var dependentStock))
            {
                var scale = 1f + Math.Pow(_player.PlayerCount, 0.8f) * dependentStock.Coefficient;
                restock = (uint) Math.Floor(amount * Math.Max(scale, 1f));
            }

            return Math.Max(restock, 1);
        }

        return component.PackPrototypeId == "SustenanceInventory" ? 1u : 2u;
    }
}

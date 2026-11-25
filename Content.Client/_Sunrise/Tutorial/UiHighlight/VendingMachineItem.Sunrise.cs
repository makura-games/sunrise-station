using Content.Client.UserInterface;
using Robust.Shared.GameObjects;

// ReSharper disable once CheckNamespace
namespace Content.Client.VendingMachines.UI;

public sealed partial class VendingMachineItem : IEntityControl
{
    EntityUid? IEntityControl.UiEntity => ItemPrototype.Entity?.Owner;
}

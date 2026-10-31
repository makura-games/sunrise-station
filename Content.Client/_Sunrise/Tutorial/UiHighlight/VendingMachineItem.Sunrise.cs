using Content.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client.VendingMachines.UI;

public sealed partial class VendingMachineItem : IEntityControl
{
    EntityUid? IEntityControl.UiEntity => ItemPrototype.Entity?.Owner;
}

using Robust.Shared.Prototypes;

#pragma warning disable IDE0130
namespace Content.Shared.VendingMachines.Components;

public sealed partial class VendingMachineComponent
{
    /// <summary>
    /// Прототип, который всегда добавляется при случайном пополнении вместо выбора из стартового запаса.
    /// </summary>
    [DataField("forceRandom")]
    public EntProtoId? RandomRestockTarget;
}

using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class VampireActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public VampirePowerLevel RequiredPowerLevel = VampirePowerLevel.Neonate;

    /// <summary>
    /// Стоимость в DrunkBlood, если явно не задана другая.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BloodCost;
}

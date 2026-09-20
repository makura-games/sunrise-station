using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Лечение вампира кровью: сколько урона по типам восстанавливается за укус.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VampireHealingComponent : Component
{
    /// <summary>
    /// Тупой урон, лечащийся за тик из крови вампира.
    /// </summary>
    [DataField]
    public int VampHealBrute = 2;

    /// <summary>
    /// Ожоговый урон, лечащийся за тик из крови вампира.
    /// </summary>
    [DataField]
    public int VampHealBurn = 2;

    /// <summary>
    /// Урон удушьем, лечащийся за тик из крови вампира.
    /// </summary>
    [DataField]
    public int VampHealAsphyxiation = 10;

    /// <summary>
    /// Ядовитый урон, лечащийся за тик из крови вампира.
    /// </summary>
    [DataField]
    public int VampHealPois = 4;
}

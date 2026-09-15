using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VampiricClawsComponent : Component
{
    /// <summary>
    /// Сколько успешных ударов в ближнем бою до исчезновения когтей
    /// </summary>
    [DataField]
    public int HitsRemaining = 15;

    /// <summary>
    /// Количество крови, высасываемое за успешный удар.
    /// </summary>
    [DataField]
    public int BloodPerHit = 5;
}

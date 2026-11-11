using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Body.Components;

/// <summary>
/// Определяет вид и пищевую ценность крови.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodSourceComponent : Component
{
    /// <summary>
    /// Вид крови.
    /// </summary>
    [DataField, AutoNetworkedField]
    public BloodType Kind = BloodType.Blood;

    /// <summary>
    /// Базовая ценность крови.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Value = 0.05f;

    /// <summary>
    /// Множитель ценности крови трупа.
    /// </summary>
    [DataField]
    public float CorpseMultiplier = 0.1f;

    /// <summary>
    /// Множители ценности по стадии гниения.
    /// </summary>
    [DataField]
    public Dictionary<int, float> RotMultipliers = new()
    {
        [0] = 1f,
        [1] = 0.5f,
        [2] = 0.25f,
        [3] = 0.1f,
        [4] = 0f,
    };
}

/// <summary>
/// Виды крови и заменяющих её жидкостей.
/// </summary>
public enum BloodType : byte
{
    Blood,
    Insect,
    Copper,
    Sap,
    Slime,
    Ammonia,
    Sulfur,
    Acid,
    Confectionery,
    Alien,
    Tainted,
}

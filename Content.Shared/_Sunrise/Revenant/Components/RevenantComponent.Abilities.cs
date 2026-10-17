using System.Numerics;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Shared.Revenant.Components;

public sealed partial class RevenantComponent
{
    /// <summary>
    /// Оглушение и материализация после применения блокировки способности.
    /// </summary>
    [DataField]
    public Vector2 LockDebuffs = new(2, 8);

    #region Drain Ability

    /// <summary>
    /// Оглушение и материализация после вытягивания эссенции.
    /// </summary>
    [DataField]
    public Vector2 DrainDebuffs = new(2, 8);

    /// <summary>
    /// Радиус вытягивания эссенции.
    /// </summary>
    [DataField]
    public float DrainRadius = 2.2f;

    /// <summary>
    /// Минимальный урон одной цели.
    /// </summary>
    [DataField]
    public int DrainDamageMin = 1;

    /// <summary>
    /// Максимальный урон одной цели.
    /// </summary>
    [DataField]
    public int DrainDamageMax = 9;

    /// <summary>
    /// Тип наносимого вытягиванием урона.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> DrainDamageType = "Cellular";

    /// <summary>
    /// Доля урона, преобразуемая в валюту.
    /// </summary>
    [DataField]
    public float StolenEssenceCurrencyRate = 0.22f;

    /// <summary>
    /// Доля урона, преобразуемая в эссенцию.
    /// </summary>
    [DataField]
    public float EssenceGainRate = 0.6f;

    #endregion
}

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

using Content.Shared._Sunrise.Antags.Vampires.Prototypes;
using Robust.Shared.Prototypes;

/// <summary>
/// Навешивается на созданную сущность действия для задания вампирских условий и стоимостей
/// - BloodToUnlock — требуемый TotalBlood для открытия действия
/// - BloodCost — количество крови на использование
/// - RequiredClass — необязательное требование класса для действия
/// - RequiresFullPower — должна ли быть достигнута полная сила
/// - AllowNonVampireUsers — позволяет напрямую выданным админ/дебаг действиям работать без VampireComponent
/// </summary>
[RegisterComponent]
public sealed partial class VampireActionComponent : Component
{
    /// <summary>
    /// Требуемый TotalBlood для открытия действия.
    /// </summary>
    [DataField]
    public int BloodToUnlock = 0;

    /// <summary>
    /// Количество крови на использование действия.
    /// </summary>
    [DataField]
    public float BloodCost = 0f;

    /// <summary>
    /// Необязательное требование класса для действия.
    /// </summary>
    [DataField]
    public ProtoId<VampireClassPrototype>? RequiredClass = null;

    /// <summary>
    /// Требуется ли достижение полной силы для использования.
    /// </summary>
    [DataField]
    public bool RequiresFullPower;

    /// <summary>
    /// Разрешает ли действие пользователям без VampireComponent.
    /// </summary>
    [DataField]
    public bool AllowNonVampireUsers = true;
}

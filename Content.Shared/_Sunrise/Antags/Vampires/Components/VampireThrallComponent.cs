using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
///     Маркерный компонент для сущностей, порабощённых вампиром Данталион.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class VampireThrallComponent : BaseMindRoleComponent
{
    /// <summary>
    ///     Вампир, который сейчас контролирует этого тхралла
    /// </summary>
    public EntityUid? Master;

    /// <summary>
    /// Текущее количество выпитой святой воды.
    /// </summary>
    [DataField]
    public FixedPoint2 HolyWaterConsumed = FixedPoint2.Zero;

    /// <summary>
    /// Количество святой воды для освобождения от контроля.
    /// </summary>
    [DataField]
    public FixedPoint2 HolyWaterToBreakFree = FixedPoint2.New(30);

    /// <summary>
    /// Реагент святой воды, освобождающий тхралла.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> HolyWaterReagentId = "Holywater";

    /// <summary>
    /// Длительность оглушения тхралла после освобождения.
    /// </summary>
    [DataField]
    public TimeSpan DeconvertStunDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Интервал проверки условий освобождения тхралла.
    /// </summary>
    [DataField]
    public TimeSpan BreakFreeCheckInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Прототип цели подчинения хозяину, выдаваемой тхраллу.
    /// </summary>
    [DataField]
    public EntProtoId ObeyObjectiveId = "VampireThrallObeyMasterObjective";

    /// <summary>
    /// Прототип роли разума тхралла.
    /// </summary>
    [DataField]
    public EntProtoId MindRoleId = "MindRoleThrall";

    /// <summary>
    /// Время следующей проверки условий освобождения тхралла.
    /// </summary>
    [ViewVariables]
    [AutoPausedField]
    public TimeSpan NextBreakFreeCheck;
}

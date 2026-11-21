using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Облако заразного аэрозоля: всё живое, что им дышит, рискует подхватить болезнь.
/// </summary>
/// <remarks>
/// Живёт в shared, чтобы прототип облака загружался на клиенте. Заражение считает сервер.
/// Передача идёт капельным путём, поэтому маски работают как обычно.
/// Дыхание из баллона снижает риск, но не убирает его: взвесь оседает на коже и глазах.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseCloudComponent : Component
{
    /// <summary>
    /// Болезнь в облаке. Проставляется тем, кто облако выпустил.
    /// </summary>
    [ViewVariables]
    public EntityUid? Disease;

    /// <summary>
    /// Радиус, в котором облако заражает.
    /// </summary>
    [DataField]
    public float Range = 2.5f;

    /// <summary>
    /// Как часто облако пробует заразить тех, кто внутри.
    /// </summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Шанс заражения за одну попытку.
    /// </summary>
    [DataField]
    public float Chance = 0.16f;

    /// <summary>
    /// Множитель шанса для того, кто дышит из баллона.
    /// </summary>
    [DataField]
    public float InternalsCoefficient = 0.5f;

    /// <summary>
    /// Заражать, даже если штамм сам по воздуху не передаётся.
    /// </summary>
    /// <remarks>
    /// Для аварий с лабораторной культурой: из разбитого сосуда наружу вырывается концентрат,
    /// которому не нужен выведенный путь передачи. Маски и баллоны защищают как обычно.
    /// </remarks>
    [DataField]
    public bool IgnoreRoutes;

    /// <summary>
    /// Когда облако попробует заразить в следующий раз.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextInfect;
}

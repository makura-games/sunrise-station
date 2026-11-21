using Content.Shared.Trigger.Components.Effects;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// При срабатывании обеззараживает очаги и мёртвые тела в радиусе: дезинфицирующая граната.
/// </summary>
/// <remarks>
/// Обычный химический дым тут не годится: он реагирует только с кровотоком живых, а заражённые
/// предметы не трогает. Живёт в shared ради загрузки прототипов, работа на сервере.
/// </remarks>
[RegisterComponent, AutoGenerateComponentState]
public sealed partial class DiseaseDisinfectOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// Радиус обработки.
    /// </summary>
    [DataField]
    public float Range = 4f;
}

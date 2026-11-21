namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Многоразовый вакцинный инжектор: в него вставляют одну вакцину и колют ею без ограничения доз.
/// </summary>
/// <remarks>
/// Вакцина в слоте не расходуется, поэтому инжектор — инструмент массовой иммунизации и открывается
/// поздним исследованием. Слот задаётся через <c>ItemSlots</c> в прототипе. Живёт в shared ради
/// загрузки прототипов, работа на сервере.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseVaccineInjectorComponent : Component
{
    /// <summary>
    /// Слот с заряженной вакциной.
    /// </summary>
    [DataField]
    public string SlotId = "vaccine";

    /// <summary>
    /// Время укола.
    /// </summary>
    [DataField]
    public TimeSpan InjectDelay = TimeSpan.FromSeconds(0.5);
}

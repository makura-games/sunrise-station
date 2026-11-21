using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Одноразовый инъектор против конкретной болезни: вакцина из вакцинатора или сыворотка из крови донора.
/// </summary>
/// <remarks>
/// Болезнь заполняется на сервере и клиенту не отдаётся. Код штамма синхронизируется:
/// он и так напечатан на этикетке, а по нему клиент предсказывает укол заряженной ампулой.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseVaccineComponent : Component
{
    /// <summary>
    /// Время инъекции.
    /// </summary>
    [DataField]
    public TimeSpan InjectDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Запускает ли препарат выздоровление у больного.
    /// </summary>
    [DataField]
    public bool Cures = true;

    /// <summary>
    /// Даёт ли препарат иммунитет здоровому.
    /// </summary>
    [DataField]
    public bool Immunizes = true;

    /// <summary>
    /// Болезнь, против которой заряжен препарат. Заполняется машиной при изготовлении.
    /// </summary>
    [ViewVariables]
    public EntityUid? Disease;

    /// <summary>
    /// Код штамма для осмотра и этикетки. Пустой у незаряженного препарата.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public string StrainCode = string.Empty;
}

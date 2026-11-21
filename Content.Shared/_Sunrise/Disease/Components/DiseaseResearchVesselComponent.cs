using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>Исследует образец выведенного штамма и отдаёт данные на подключённый сервер.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class DiseaseResearchVesselComponent : Component
{
    /// <summary>Слот с образцом для исследования.</summary>
    [DataField]
    public string SampleSlotId = "researchVesselSample";

    /// <summary>Правила вставки и извлечения образца.</summary>
    [DataField]
    public ItemSlot SampleSlot = new();

    /// <summary>Сколько очков исследования в секунду даёт единица силы штамма.</summary>
    [DataField]
    public float PointsPerStrength = 0.5f;

    /// <summary>Максимум очков исследования в секунду.</summary>
    [DataField]
    public int MaxPointsPerSecond = 30;

    /// <summary>Сколько секунд штамм даёт очки при нулевой устойчивости; запас общий на все копии.</summary>
    [DataField]
    public int MinStudySeconds = 300;

    /// <summary>Сколько секунд штамм даёт очки при максимальной устойчивости.</summary>
    [DataField]
    public int MaxStudySeconds = 1200;

    /// <summary>
    /// Облако, которое вырывается из разрушенного сосуда с культурой.
    /// </summary>
    [DataField]
    public EntProtoId BreachCloudPrototype = "SunriseDiseaseCloudBreach";

    /// <summary>Лежит ли в сосуде штамм, пригодный для исследования.</summary>
    [AutoNetworkedField]
    public bool HasSample;

    /// <summary>Код вставленного штамма, показывается при осмотре.</summary>
    [AutoNetworkedField]
    public string StrainCode = string.Empty;

    /// <summary>Сила вставленного штамма, посчитанная сервером.</summary>
    [AutoNetworkedField]
    public int Strength;

    /// <summary>Устойчивость вставленного штамма в процентах.</summary>
    [AutoNetworkedField]
    public int ResistancePercent;

    /// <summary>Номинальная выработка до общего модификатора очков исследования.</summary>
    [AutoNetworkedField]
    public int PointsPerSecond;

    /// <summary>Сколько секунд исследования осталось; запас общий на все копии штамма.</summary>
    [AutoNetworkedField]
    public int RemainingSeconds;

    /// <summary>Остановлено ли исследование из-за питания или потери связи с сервером.</summary>
    [AutoNetworkedField]
    public bool StudyPaused;
}

using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// При срабатывании выбрасывает облака заразного аэрозоля.
/// </summary>
/// <remarks>
/// Штамм заводится при первом срабатывании и закрепляется за устройством: вся вспышка
/// от одной гранаты — одна болезнь, а вторая такая же граната даст другую.
/// Живёт в shared только ради загрузки прототипов, вся работа на сервере.
/// </remarks>
[RegisterComponent, AutoGenerateComponentState]
public sealed partial class DiseaseCloudOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>Кандидаты, из которых выбирается болезнь устройства.</summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    /// <summary>Прототип облака.</summary>
    [DataField]
    public EntProtoId CloudPrototype = "SunriseDiseaseCloud";

    /// <summary>Сколько облаков выбрасывается за одно срабатывание.</summary>
    [DataField]
    public int Clouds = 6;

    /// <summary>В каком радиусе расходятся облака.</summary>
    [DataField]
    public float SpreadRange = 3.5f;

    /// <summary>Выбрасывается ли аэрозоль ещё и при открытии контейнера.</summary>
    [DataField]
    public bool ReleaseOnOpen;

    /// <summary>Выбрасывает ли источник аэрозоль только один раз.</summary>
    [DataField]
    public bool SingleUse;

    /// <summary>Выбросил ли источник свой аэрозоль.</summary>
    public bool Released;

    /// <summary>Необязательный образец выпущенного штамма: его оставляет биологическая находка.</summary>
    [DataField]
    public EntProtoId? SamplePrototype;

    /// <summary>Штамм этого устройства, заводится при первом срабатывании.</summary>
    [ViewVariables]
    public EntityUid? Strain;
}

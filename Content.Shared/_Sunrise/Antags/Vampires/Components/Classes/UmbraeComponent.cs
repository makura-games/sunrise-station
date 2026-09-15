using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Sunrise.Antags.Vampires.Components.Classes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[AutoGenerateComponentState]
public sealed partial class UmbraeComponent : VampireClassComponent
{
    /// <summary>
    /// Активен ли Плащ тьмы.
    /// </summary>
    [AutoNetworkedField]
    public bool CloakOfDarknessActive = false;

    /// <summary>
    /// Порог TotalBlood, после которого питьё крови гасит свет рядом.
    /// </summary>
    [DataField]
    public int BreakLightBloodThreshold = 300;

    /// <summary>
    /// Радиус поиска источников света для гашения.
    /// </summary>
    [DataField]
    public float BreakLightRange = 8f;

    /// <summary>
    /// Радиус, в котором гуманоиды раскрывают вампира под Плащом тьмы.
    /// </summary>
    [DataField]
    public float CloakOfDarknessRevealRange = 4.5f;

    /// <summary>
    /// Минимальная видимость под Плащом тьмы вблизи наблюдателя.
    /// </summary>
    [DataField]
    public float CloakOfDarknessMinVisibility = -0.8f;

    /// <summary>
    /// Максимальная видимость под Плащом тьмы вдали от наблюдателя.
    /// </summary>
    [DataField]
    public float CloakOfDarknessMaxVisibility = 0.6f;

    /// <summary>
    /// Интервал пересчёта видимости Плаща тьмы.
    /// </summary>
    [DataField]
    public TimeSpan CloakOfDarknessVisibilityUpdateInterval = TimeSpan.FromSeconds(0.15);

    /// <summary>
    /// Время следующего пересчёта видимости Плаща тьмы.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextCloakOfDarknessVisibilityUpdate;

    /// <summary>
    /// Был ли у вампира StealthComponent до активации Плаща тьмы.
    /// </summary>
    public bool CloakHadStealthComponent;

    /// <summary>
    /// Предыдущее состояние включённости StealthComponent.
    /// </summary>
    public bool CloakPreviousStealthEnabled;

    /// <summary>
    /// Предыдущее значение видимости StealthComponent.
    /// </summary>
    public float CloakPreviousStealthVisibility = 1f;

    /// <summary>
    /// Активна ли Вечная тьма.
    /// </summary>
    [AutoNetworkedField]
    public bool EternalDarknessActive = false;
    /// <summary>
    /// Сущность ауры Вечной тьмы, привязанная к вампиру.
    /// </summary>
    public EntityUid? EternalDarknessAuraEntity = null;
    /// <summary>
    /// Активен ли Теневой бокс.
    /// </summary>
    [AutoNetworkedField]
    public bool ShadowBoxingActive = false;

    /// <summary>
    /// Текущая цель Теневого бокса.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? ShadowBoxingTarget = null;
    /// <summary>
    /// Время окончания Теневого бокса.
    /// </summary>
    public TimeSpan? ShadowBoxingEndTime = null;
    /// <summary>
    /// Флаг выполнения цикла урона Теневого бокса.
    /// </summary>
    public bool ShadowBoxingLoopRunning = false;
    /// <summary>
    /// Идентификатор цикла Вечной тьмы против дублирующих циклов.
    /// </summary>
    public int EternalDarknessLoopId = 0;

    /// <summary>
    /// Сущность установленного маяка Теневого якоря.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? SpawnedShadowAnchorBeacon = null;

    /// <summary>
    /// Время автоматического возврата к Теневому якорю.
    /// </summary>
    [AutoPausedField]
    public TimeSpan? ShadowAnchorAutoReturnTime;

    /// <summary>
    /// Идёт ли сейчас установка Теневого якоря.
    /// </summary>
    public bool ShadowAnchorPlacementInProgress;
    /// <summary>
    /// Идентификатор цикла Теневого якоря против дублирующих циклов.
    /// </summary>
    public int ShadowAnchorLoopId;

    /// <summary>
    /// Список установленных теневых ловушек
    /// </summary>
    public List<EntityUid> PlacedSnares = new();

    /// <summary>
    /// Максимальное количество устанавливаемых теневых ловушек
    /// </summary>
    [DataField]
    public int MaxSnares = 3;
}

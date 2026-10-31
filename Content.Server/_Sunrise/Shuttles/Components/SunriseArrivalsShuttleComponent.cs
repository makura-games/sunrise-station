using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Sunrise.Shuttles.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SunriseArrivalsShuttleComponent : Component
{
    /// <summary>
    /// Станция, к которой направляется этот шаттл.
    /// </summary>
    public EntityUid Station;

    /// <summary>
    /// Моб игрока, сейчас находящийся на шаттле.
    /// </summary>
    public EntityUid? Player;

    /// <summary>
    /// Текущее состояние жизненного цикла шаттла.
    /// </summary>
    public SunriseArrivalsShuttleState State = SunriseArrivalsShuttleState.Queued;

    /// <summary>
    /// Время создания шаттла для таймаута аварийной защиты.
    /// </summary>
    [AutoPausedField]
    public TimeSpan SpawnTime;

    /// <summary>
    /// Время стыковки шаттла со станцией.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? DockTime;

    /// <summary>
    /// Сущность сопровождающего на шаттле, которая дает голосовые ответы.
    /// </summary>
    public EntityUid? Attendant;

    /// <summary>
    /// Был ли игрок уже поприветствован сопровождающим.
    /// </summary>
    public bool Greeted;

    /// <summary>
    /// Сохраненное имя игрока для приветственного сообщения.
    /// </summary>
    public string PlayerName = string.Empty;

    /// <summary>
    /// Сохраненное название должности для приветственного сообщения.
    /// </summary>
    public string PlayerJob = string.Empty;

    /// <summary>
    /// Время, когда нужно отправить приветственное сообщение.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? GreetTime;

    /// <summary>
    /// Было ли уже выдано предупреждение об эвакуации.
    /// </summary>
    public bool Warned;

    /// <summary>
    /// Предупреждал ли стюард игрока о заблокированных docks.
    /// </summary>
    public bool DockBlockedWarned;

    /// <summary>
    /// Время, когда шаттл начал улетать, для отложенного удаления.
    /// </summary>
    [AutoPausedField]
    public TimeSpan? LeaveStartTime;

    /// <summary>
    /// Время, когда игрок в последний раз был замечен вне шаттла.
    /// Используется для короткой льготной паузы перед отправлением, чтобы шлюз не раздавил игрока.
    /// </summary>
    [AutoPausedField]
    public TimeSpan? PlayerExitTime;

    /// <summary>
    /// Docks, зарезервированные этим шаттлом на целевой станции.
    /// </summary>
    public HashSet<EntityUid> ReservedDocks = new();
}

public enum SunriseArrivalsShuttleState : byte
{
    Queued,
    Travelling,
    Docked,
    Leaving,
}

namespace Content.Server._Sunrise.Shuttles.Components;

/// <summary>
/// Singleton-компонент на общей карте пула, где спавнятся все шаттлы прибытия.
/// Вместо отдельной карты на игрока шаттлы размещаются на одной карте
/// со смещением по X.
/// </summary>
[RegisterComponent]
public sealed partial class SunriseArrivalsPoolComponent : Component
{
    /// <summary>
    /// Следующее смещение по X на карте пула для спавна очередного grid шаттла.
    /// </summary>
    public float NextOffset;

    /// <summary>
    /// Упорядоченная очередь EntityUid шаттлов, ожидающих отправки на станцию.
    /// </summary>
    public List<EntityUid> Queue = new();

    /// <summary>
    /// Время следующей проверки очереди на свободные доки
    /// </summary>
    public TimeSpan NextDispatchTime;

    /// <summary>
    /// Последнее время, когда станции сообщали о заблокированных доках прибытия.
    /// </summary>
    public TimeSpan LastAlertTime;
}

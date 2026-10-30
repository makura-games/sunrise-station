namespace Content.Client._Sunrise.Light.Visualizers;

/// <summary>
/// Локальное состояние эффекта; не передаётся серверу или другим клиентам.
/// </summary>
[RegisterComponent]
public sealed partial class SunrisePoweredLightSparksVisualsComponent : Component
{
    /// <summary>
    /// Выбранное клиентом поведение текущей разбитой лампы.
    /// </summary>
    public bool? ShouldFlicker;

    /// <summary>
    /// Энергия лампы до вспышки для восстановления при остановке анимации.
    /// </summary>
    public float? OriginalLightEnergy;

    /// <summary>
    /// Локальный звук текущей вспышки.
    /// </summary>
    public EntityUid? FlickerAudio;
}

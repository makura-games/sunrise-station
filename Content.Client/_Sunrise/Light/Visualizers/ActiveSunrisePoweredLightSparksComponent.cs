namespace Content.Client._Sunrise.Light.Visualizers;

/// <summary>
/// Клиентский таймер мерцания разбитого запитанного светильника.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveSunrisePoweredLightSparksComponent : Component
{
    /// <summary>
    /// Время следующей вспышки с учётом паузы карты.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextFlickerTime;
}

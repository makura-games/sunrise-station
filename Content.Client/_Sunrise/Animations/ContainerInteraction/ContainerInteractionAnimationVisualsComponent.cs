namespace Content.Client._Sunrise.Animations.ContainerInteraction;

/// <summary>
/// Компонент, создающий анимации взаимодействия с контейнерами.
/// Создает анимацию при добавлении и изъятии предмета из контейнера.
/// </summary>
[RegisterComponent]
public sealed partial class ContainerInteractionAnimationVisualsComponent : Component
{
    /// <summary>
    /// Множитель масштаба поверх базы спрайта
    /// </summary>
    [DataField]
    public float Scale = 1.1f;

    /// <summary>
    /// Случайный параметр добавляемый к <see cref="Scale"/>.
    /// Выбирается в диапозоне [-ЗНАЧЕНИЕ, ЗНАЧЕНИЕ]
    /// </summary>
    [DataField]
    public float ScaleVariation = 0.15f;

    /// <summary>
    /// Длительность анимации.
    /// </summary>
    [DataField]
    public float Duration = 0.2f;
}

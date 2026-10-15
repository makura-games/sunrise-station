namespace Content.Server._Sunrise.BodyCamera;

/// Добавляет бодикамере действие «Паника» в стандартном меню взаимодействий.
[RegisterComponent]
public sealed partial class BodyCameraPanicComponent : Component
{
    /// Минимальный интервал между вызовами с одной камеры.
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    /// Время следующего допустимого вызова. Хранится только на сервере.
    [ViewVariables]
    public TimeSpan NextActivation;
}

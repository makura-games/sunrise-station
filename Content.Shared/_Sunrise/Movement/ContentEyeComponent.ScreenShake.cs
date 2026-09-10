// Файл намеренно расположен в _Sunrise, но расширяет ванильный partial-компонент.
#pragma warning disable IDE0130
namespace Content.Shared.Movement.Components;

public sealed partial class ContentEyeComponent
{
    /// <summary>
    /// Постоянный поворот камеры, поверх которого накладываются временные эффекты.
    /// </summary>
    [DataField]
    public Angle BaseRotation = Angle.Zero;
}

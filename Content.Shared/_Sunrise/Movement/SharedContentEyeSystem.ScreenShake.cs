using Content.Shared._Sunrise.Camera;
using Content.Shared.Movement.Components;

// Файл намеренно расположен в _Sunrise, но расширяет ванильный partial-класс.
#pragma warning disable IDE0130
namespace Content.Shared.Movement.Systems;

public abstract partial class SharedContentEyeSystem
{
    /// <summary>
    /// Обновляет постоянный угол камеры, поверх которого накладываются временные эффекты.
    /// </summary>
    public bool SetBaseRotation(Entity<ContentEyeComponent?> eye, Angle rotation)
    {
        if (!Resolve(eye, ref eye.Comp, false))
            return false;

        eye.Comp.BaseRotation = rotation;
        return true;
    }

    /// <summary>
    /// Пересчитывает поворот камеры с учётом временных эффектов.
    /// </summary>
    public void UpdateEyeRotation(Entity<EyeComponent> eye)
    {
        var baseRotation = Angle.Zero;
        if (TryComp<ContentEyeComponent>(eye, out var contentEye))
            baseRotation = contentEye.BaseRotation;

        var ev = new GetEyeRotationEvent();
        RaiseLocalEvent(eye, ref ev);

        _eye.SetRotation(eye, baseRotation + ev.Rotation, eye);
    }
}

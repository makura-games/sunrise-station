using Content.Shared.Movement.Systems;

namespace Content.Shared._Sunrise.Camera;

/// <summary>
/// Направленное событие для суммирования временных изменений поворота камеры.
/// </summary>
/// <remarks>
/// Вызывается из <see cref="SharedContentEyeSystem.UpdateEyeRotation"/> и дополняет базовый поворот глаза.
/// </remarks>
[ByRefEvent]
public record struct GetEyeRotationEvent(Angle Rotation);

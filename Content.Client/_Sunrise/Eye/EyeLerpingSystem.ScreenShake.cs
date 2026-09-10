using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;

// Файл намеренно расположен в _Sunrise, но расширяет ванильный partial-класс.
#pragma warning disable IDE0130
namespace Content.Client.Eye;

public sealed partial class EyeLerpingSystem
{
    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;

    private void SetSunriseBaseRotation(EntityUid uid, Angle rotation, EyeComponent eye)
    {
        if (_contentEye.SetBaseRotation((uid, null), rotation))
            return;

        _eye.SetRotation(uid, rotation, eye);
    }
}

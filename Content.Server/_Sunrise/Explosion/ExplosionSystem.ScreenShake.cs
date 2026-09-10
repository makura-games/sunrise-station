using Content.Shared._Sunrise.Camera;
using Robust.Shared.Map;
using Robust.Shared.Player;

// Файл намеренно расположен в _Sunrise, но расширяет ванильный partial-класс.
#pragma warning disable IDE0130
namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    [Dependency] private readonly SunriseScreenShakeSystem _sunriseScreenShake = default!;

    private void AddSunriseExplosionScreenShake(
        int iterationCount,
        MapCoordinates epicenter,
        QueuedExplosion queued)
    {
        var range = iterationCount * 4f;
        if (range <= 0f)
            return;

        var recipients = Filter.Empty();
        recipients.AddInRange(epicenter, range, _playerManager, EntityManager);

        var translation = iterationCount < queued.Proto.SmallSoundIterationThreshold
            ? new SunriseScreenShakeParameters
            {
                Trauma = 0.4f,
                DecayRate = 0.2f,
                Frequency = 0.014f,
            }
            : new SunriseScreenShakeParameters
            {
                Trauma = 0.6f,
                DecayRate = 0.05f,
                Frequency = 0.014f,
            };

        foreach (var recipient in recipients.Recipients)
        {
            if (recipient.AttachedEntity is not { } uid)
                continue;

            _sunriseScreenShake.Shake(uid, translation, null);
        }
    }
}

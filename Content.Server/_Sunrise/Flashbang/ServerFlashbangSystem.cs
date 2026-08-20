using Content.Server.Atmos.EntitySystems;
using Content.Shared._Sunrise.Flashbang;
using Content.Shared.Mobs.Components;

namespace Content.Server._Sunrise.Flashbang;

/// <summary>
/// Серверное расширение: отменяет эффект вспышки в условиях низкого атмосферного давления.
/// </summary>
public sealed class ServerFlashbangSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateComponent, FlashbangAttemptEvent>(OnFlashbangAttempt);
    }

    private void OnFlashbangAttempt(EntityUid uid, MobStateComponent _, ref FlashbangAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<FlashbangRadiusOnTriggerComponent>(args.Source, out var flashComp))
            return;

        // null означает отсутствие атмосферы (открытый космос)
        var mixture = _atmos.GetContainingMixture(args.Source);
        if (mixture == null || mixture.Pressure < flashComp.MinAmbientPressure)
            args.Cancelled = true;
    }
}

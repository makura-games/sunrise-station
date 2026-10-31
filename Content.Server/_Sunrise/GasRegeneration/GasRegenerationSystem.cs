using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Robust.Shared.Timing;

namespace Content.Server.Sunrise.GasRegeneration;

public sealed partial class GasRegenerationSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AtmosphereSystem _atmosphereSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<_Sunrise.GasRegeneration.GasRegenerationComponent, EntityUnpausedEvent>(OnUnpaused);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<_Sunrise.GasRegeneration.GasRegenerationComponent, GasTankComponent>();
        while (query.MoveNext(out var uid, out var gasRegen, out var gasTank))
        {
            if (_timing.CurTime < gasRegen.NextRegenTime)
                continue;

            gasRegen.NextRegenTime = _timing.CurTime + gasRegen.Duration;
            _atmosphereSystem.Merge(gasTank.Air, gasRegen.AirRegen.Clone());
        }
    }

    private void OnUnpaused(EntityUid uid, _Sunrise.GasRegeneration.GasRegenerationComponent comp, ref EntityUnpausedEvent args)
    {
        comp.NextRegenTime += args.PausedTime;
    }
}

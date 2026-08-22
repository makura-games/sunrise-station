using Content.Shared._RMC14.Explosion.Components;
using Robust.Shared.Timing;

namespace Content.Client._RMC14._Sunrise.Explosion;

public sealed partial class ClientExplosionShockWaveSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCExplosionShockWaveComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<RMCExplosionShockWaveComponent> ent, ref ComponentInit args)
    {
        ent.Comp.CreationTime = _timing.CurTime;
    }
}

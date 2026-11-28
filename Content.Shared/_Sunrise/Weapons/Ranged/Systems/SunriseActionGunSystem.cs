using Content.Shared._Sunrise.Weapons.Ranged.Components;
using Content.Shared.Actions;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Sunrise.Weapons.Ranged.Systems;

/// <summary>
/// Manages action-driven internal guns used by Sunrise NPCs.
/// </summary>
public sealed partial class SunriseActionGunSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseActionGunComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SunriseActionGunComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SunriseActionGunComponent, SunriseActionGunShootEvent>(OnShoot);
    }

    private void OnMapInit(Entity<SunriseActionGunComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        ent.Comp.Gun = Spawn(ent.Comp.GunProto);
    }

    private void OnShutdown(Entity<SunriseActionGunComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Gun is { } gun)
            QueueDel(gun);
    }

    private void OnShoot(Entity<SunriseActionGunComponent> ent, ref SunriseActionGunShootEvent args)
    {
        if (TryComp<GunComponent>(ent.Comp.Gun, out var gun))
            _gun.AttemptShoot(ent, (ent.Comp.Gun.Value, gun), args.Target);
    }
}

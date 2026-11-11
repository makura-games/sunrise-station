using Content.Shared._Sunrise.Boss.Components;
using Content.Shared._Sunrise.Boss.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Boss.Systems;

public sealed partial class HellSpawnInvincibilitySystem : SharedHellSpawnInvincibilitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HellSpawnInvincibilityComponent, DamageChangedEvent>(OnDamageReceived);
        SubscribeLocalEvent<HellSpawnInvincibilityComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<HellSpawnInvincibilityComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.InvincibilityActionEntity, ent.Comp.InvincibilityAction);
    }

    private void OnDamageReceived(Entity<HellSpawnInvincibilityComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (ent.Comp.InvincibilityActionEntity == null)
            return;
        if (!TryComp<ActionComponent>(ent.Comp.InvincibilityActionEntity, out var action))
            return;
        if (!TryComp<InstantActionComponent>(ent.Comp.InvincibilityActionEntity, out var instantAction))
            return;
        if (!_actions.ValidAction((ent.Comp.InvincibilityActionEntity.Value, action)))
            return;

        _actions.PerformAction((ent.Owner, null),
            (ent.Comp.InvincibilityActionEntity.Value, action),
            instantAction.Event,
            false);
    }
}

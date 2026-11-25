using Content.Server.Construction;
using Content.Shared.Damage.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Sunrise.TradeStation;

public sealed class SunriseTradeStationComputerSystem : EntitySystem
{
    [Dependency] private ConstructionSystem _construction = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SunriseTradeStationComputerComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<SunriseTradeStationComputerComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta != null && args.DamageDelta.GetTotal() >= ent.Comp.DestructionDamage)
        {
            if (_container.TryGetContainer(ent, "board", out var board))
            {
                foreach (var contained in board.ContainedEntities)
                    QueueDel(contained);
            }

            QueueDel(ent);
            return;
        }

        if (_damageable.GetTotalDamage((ent.Owner, args.Damageable)) < ent.Comp.BreakDamage)
            return;

        _construction.ChangeNode(ent, null, "monitorBroken");
    }
}

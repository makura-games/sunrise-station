using Content.Shared.CombatMode;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Trigger.Systems;

namespace Content.Server._Sunrise.Electropack;

/// <summary>
/// Система отслеживает включение боевого режима и запускает триггер на рюкзаке,
/// если на носителе надет рюкзак с <see cref="TriggerOnCombatModeEnabledComponent"/>.
/// Шок применяется через уже существующий <see cref="ShockOnTriggerComponent"/>,
/// после чего боевой режим принудительно отключается.
/// </summary>
public sealed class TriggerOnCombatModeEnabledSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CombatModeComponent, CombatModeChangedEvent>(OnCombatModeChanged);
    }

    private void OnCombatModeChanged(Entity<CombatModeComponent> ent, ref CombatModeChangedEvent args)
    {
        if (!args.IsInCombatMode)
            return;

        if (!_mobState.IsAlive(ent))
            return;

        if (!_inventory.TryGetSlotEntity(ent, "back", out var backpack))
            return;

        if (!TryComp<TriggerOnCombatModeEnabledComponent>(backpack, out var triggerComp))
            return;

        _trigger.Trigger(backpack.Value, ent);

        if (triggerComp.DisableCombatModeAfterTrigger)
            _combatMode.SetInCombatMode(ent, false, ent.Comp);
    }
}

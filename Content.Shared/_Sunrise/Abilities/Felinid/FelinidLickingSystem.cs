using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Shared._Sunrise.Abilities.Felinid;

public sealed partial class FelinidLickingSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FelinidLickingComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FelinidLickingComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<FelinidLickingComponent, LickingWoundsTargetActionEvent>(OnLickingAction);
        SubscribeLocalEvent<FelinidLickingComponent, FelinidLickingDoAfterEvent>(OnDoAfter);
    }

    private void OnStartup(Entity<FelinidLickingComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.Action, ent.Comp.ActionLickingWoundsId);
        Dirty(ent);
    }

    private void OnShutdown(Entity<FelinidLickingComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.Action);
    }

    private void OnLickingAction(Entity<FelinidLickingComponent> ent, ref LickingWoundsTargetActionEvent args)
    {
        if (args.Handled) // мда
            return;

        if (!CanLick(ent, args.Target, out var errorMessage))
        {
            if (errorMessage != null)
                _popup.PopupClient(errorMessage, ent, ent);

            return;
        }

        args.Handled = TryStartLicking(ent, args.Target);
    }

    private bool TryStartLicking(Entity<FelinidLickingComponent> ent, EntityUid target)
    {
        _audio.PlayPredicted(ent.Comp.HealingBeginSound, ent, ent);

        var doAfterArgs = new DoAfterArgs(EntityManager, ent, ent.Comp.Delay, new FelinidLickingDoAfterEvent(), ent, target: target)
        {
            BreakOnMove = true,
            NeedHand = false,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(Entity<FelinidLickingComponent> ent, ref FelinidLickingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (!TryComp<DamageableComponent>(target, out var damageable))
            return;

        _damageable.TryChangeDamage(target, ent.Comp.Damage, true, origin: ent);

        if (ent.Comp.StopBleeding && TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            var wasBleeding = bloodstream.BleedAmount > 0;
            _bloodstream.TryModifyBleedAmount((target, bloodstream), ent.Comp.BloodlossModifier);

            if (wasBleeding && bloodstream.BleedAmount <= 0)
            {
                var popup = ent.Owner == target
                    ? Loc.GetString("medical-item-stop-bleeding-self")
                    : Loc.GetString("medical-item-stop-bleeding", ("target", Identity.Entity(target, EntityManager)));
                _popup.PopupClient(popup, target, ent);
            }
        }

        _audio.PlayPredicted(ent.Comp.HealingEndSound, ent, ent);

        if (_mobState.IsAlive(target) && HasDamageToHeal(target, damageable, ent.Comp))
            TryStartLicking(ent, target);

        args.Handled = true;
    }

    /// <summary>
    /// Проверяет, можно ли облизывать раны цели
    /// </summary>
    /// <param name="ent">Тот, кто облизывает</param>
    /// <param name="target">Цель</param>
    /// <param name="errorMessage">Сообщение об ошибке (если есть)</param>
    /// <returns>True, если можно облизывать</returns>
    private bool CanLick(Entity<FelinidLickingComponent> ent, EntityUid target, out string? errorMessage)
    {
        errorMessage = null;

        if (_standing.IsDown(ent.Owner))
            return false;

        if (!TryComp<DamageableComponent>(target, out var damageable))
            return false;

        if (!_mobState.IsAlive(target))
            return false;

        if (HasIngestionBlocker(ent))
        {
            errorMessage = Loc.GetString("felinid-licking-blocked-by-blocker");
            return false;
        }

        if (HasInnerOrOuterClothing(target))
        {
            errorMessage = Loc.GetString("felinid-licking-blocked-by-clothing");
            return false;
        }

        if (!HasDamageToHeal(target, damageable, ent.Comp))
            return false;

        return true;
    }

    /// <summary>
    /// Проверяет, есть ли у сущности урон, который можно вылечить
    /// </summary>
    private bool HasDamageToHeal(EntityUid target, DamageableComponent damageable, FelinidLickingComponent licking)
    {
        var damage = _damageable.GetAllDamage((target, damageable));
        foreach (var (type, _) in licking.Damage.DamageDict)
        {
            if (damage.DamageDict.TryGetValue(type, out var currentDamage) &&
                currentDamage > FixedPoint2.Zero)
            {
                return true;
            }
        }

        if (licking.StopBleeding && TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            if (bloodstream.BleedAmount > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Проверяет, есть ли у сущности блокировщик приема пищи (маска или шлем)
    /// </summary>
    private bool HasIngestionBlocker(EntityUid uid)
    {
        if (!TryComp<InventoryComponent>(uid, out var inventory))
            return false;

        var enumerator = _inventory.GetSlotEnumerator((uid, inventory), SlotFlags.MASK | SlotFlags.HEAD);
        while (enumerator.NextItem(out var item))
        {
            if (TryComp<IngestionBlockerComponent>(item, out var blocker) && blocker.Enabled)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Проверяет, есть ли у сущности внутренняя или внешняя одежда
    /// </summary>
    private bool HasInnerOrOuterClothing(EntityUid uid)
    {
        if (!TryComp<InventoryComponent>(uid, out var inventory))
            return false;

        var enumerator = _inventory.GetSlotEnumerator((uid, inventory), SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING);
        return enumerator.NextItem(out _);
    }
}

[Serializable, NetSerializable]
public sealed partial class FelinidLickingDoAfterEvent : SimpleDoAfterEvent
{
}

public sealed partial class LickingWoundsTargetActionEvent : EntityTargetActionEvent
{
}

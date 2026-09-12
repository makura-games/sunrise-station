using System.Linq;
using Content.Server.Charges;
using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Charges.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Выдача и настройка действий.

    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly ChargesSystem _charges = default!;


    private void OnActionsComponentStartup(Entity<ActionsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<VampireComponent>(ent, out var vampire))
            return;

        SyncVampireActions((ent.Owner, vampire));
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!TryComp<VampireComponent>(ev.Entity, out var vampire))
            return;

        SyncVampireActions((ev.Entity, vampire));
    }

    private void SyncVampireActions(Entity<VampireComponent> ent)
    {
        if (!TryComp<VampireConfigurationComponent>(ent, out var configuration))
            return;

        EnsureRejuvenateUpgrade(ent, configuration);
        RefreshAllActions(ent, configuration);
    }

    private void EnsureRejuvenateUpgrade(
        Entity<VampireComponent> ent,
        VampireConfigurationComponent configuration)
    {
        var upgradedActionId = configuration.RejuvenateUpgradedAction;
        var baseActionId = configuration.RejuvenateAction;
        var requiredPowerLevel = GetRequiredPowerLevel(upgradedActionId);
        if (ent.Comp.PowerLevel < requiredPowerLevel)
            return;

        var baseAction = FindVampireAction(ent.Owner, baseActionId);
        var upgradedAction = FindVampireAction(ent.Owner, upgradedActionId);
        VampireActionChargeState? previousChargeState = null;
        if (baseAction is { } firstAction)
            previousChargeState = CaptureActionChargeState(firstAction);

        if (upgradedAction is not { } secondAction)
        {
            var actionEntity = _actions.AddAction(ent.Owner, upgradedActionId, ent.Owner);
            if (actionEntity is not { } addedAction)
                return;

            ConfigureVampireAction(ent, configuration, upgradedActionId, addedAction, previousChargeState);
        }
        else
        {
            TryRefreshVampireAction(ent, configuration, upgradedActionId, secondAction);
        }

        if (baseAction is not { } actionToRemove)
            return;

        _actions.RemoveAction(ent.Owner, actionToRemove.AsNullable());
    }

    private void RefreshAllActions(
        Entity<VampireComponent> ent,
        VampireConfigurationComponent configuration)
    {
        foreach (var action in _actions.GetActions(ent.Owner))
        {
            if (action.Comp.Container != ent.Owner ||
                !HasComp<VampireActionComponent>(action.Owner) ||
                Prototype(action) is not { } prototype)
            {
                continue;
            }

            TryRefreshVampireAction(ent, configuration, prototype.ID, action);
        }
    }

    private void GrantBaseActions(
        Entity<VampireComponent> ent,
        VampireConfigurationComponent configuration)
    {
        foreach (var actionId in configuration.BaseActions)
        {
            _actions.AddAction(ent.Owner, actionId, ent.Owner);
        }
    }

    private void RemoveVampireActions(Entity<VampireComponent> ent)
    {
        foreach (var action in _actions.GetActions(ent.Owner).ToArray())
        {
            if (action.Comp.Container != ent.Owner ||
                !HasComp<VampireActionComponent>(action.Owner))
                continue;

            _actions.RemoveAction(ent.Owner, action.AsNullable());
        }
    }

    private Entity<ActionComponent>? FindVampireAction(EntityUid holder, EntProtoId actionId)
    {
        foreach (var action in _actions.GetActions(holder))
        {
            if (action.Comp.Container == holder &&
                HasComp<VampireActionComponent>(action.Owner) &&
                Prototype(action)?.ID == actionId.Id)
            {
                return action;
            }
        }

        return null;
    }

    private void TryRefreshVampireAction(
        Entity<VampireComponent> ent,
        VampireConfigurationComponent configuration,
        EntProtoId actionId,
        EntityUid? actionEntity)
    {
        if (actionEntity is null || _actions.GetAction(actionEntity) is not { } action)
            return;

        if (!TryComp<VampireActionComponent>(actionEntity.Value, out var vampireAction))
        {
            _actions.SetEnabled(action.AsNullable(), true);
            return;
        }

        ConfigureVampireAction(ent, configuration, actionId, actionEntity.Value);
        _actions.SetEnabled(action.AsNullable(), ent.Comp.PowerLevel >= vampireAction.RequiredPowerLevel);
    }

    private void ConfigureVampireAction(
        Entity<VampireComponent> ent,
        VampireConfigurationComponent configuration,
        EntProtoId actionId,
        EntityUid actionEntity,
        VampireActionChargeState? previousChargeState = null)
    {
        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return;

        VampireActionChargeSettings? chargeSettings = null;

        if (actionId == configuration.FangsAction)
        {
            _actions.SetUseDelay(actionEntity, configuration.FangsUseDelay);
        }
        else if (actionId == configuration.GlareAction)
        {
            chargeSettings = level.Glare.Action;
        }
        else if (actionId == configuration.SleepAction)
        {
            chargeSettings = level.Sleep.Action;

            if (TryComp<VampireActionComponent>(actionEntity, out var vampireAction))
                vampireAction.BloodCost = level.Sleep.BloodCost;

            _actions.SetRange(actionEntity, level.Sleep.TargetRange);
        }
        else if (actionId == configuration.RejuvenateAction ||
                 actionId == configuration.RejuvenateUpgradedAction)
        {
            chargeSettings = level.Rejuvenation.Action;
        }

        if (chargeSettings is null)
            return;

        _actions.SetUseDelay(actionEntity, chargeSettings.UseDelay);
        ConfigureActionCharges(actionEntity, chargeSettings, previousChargeState);
    }

    private void ConfigureActionCharges(
        EntityUid actionEntity,
        VampireActionChargeSettings settings,
        VampireActionChargeState? previousChargeState = null)
    {
        if (!TryComp<LimitedChargesComponent>(actionEntity, out var charges) ||
            !TryComp<AutoRechargeComponent>(actionEntity, out var recharge))
        {
            return;
        }

        var previous = previousChargeState ?? CaptureActionChargeState(actionEntity);
        var addedCapacity = Math.Max(0, settings.MaxCharges - previous.MaxCharges);
        var newCharges = Math.Clamp(previous.CurrentCharges + addedCapacity, 0, settings.MaxCharges);

        _charges.SetMaxCharges((actionEntity, charges), settings.MaxCharges);
        _charges.SetCharges((actionEntity, charges), newCharges);
        _charges.SetRechargeDuration(
            (actionEntity, charges, recharge),
            settings.RechargeDuration,
            newCharges < settings.MaxCharges ? previous.RechargeProgress : 0f);
    }

    private VampireActionChargeState CaptureActionChargeState(EntityUid actionEntity)
    {
        if (!TryComp<LimitedChargesComponent>(actionEntity, out var charges) ||
            !TryComp<AutoRechargeComponent>(actionEntity, out var recharge))
        {
            return new VampireActionChargeState(0, 0, 0f);
        }

        var currentCharges = _charges.GetCurrentCharges((actionEntity, charges, recharge));
        var progress = 0f;

        if (currentCharges >= charges.MaxCharges || recharge.RechargeDuration <= TimeSpan.Zero)
            return new VampireActionChargeState(currentCharges, charges.MaxCharges, progress);

        var elapsed = _timing.CurTime - charges.LastUpdate;
        var elapsedTicks = Math.Max(0L, elapsed.Ticks % recharge.RechargeDuration.Ticks);
        progress = Math.Clamp((float)elapsedTicks / recharge.RechargeDuration.Ticks, 0f, 1f);

        return new VampireActionChargeState(currentCharges, charges.MaxCharges, progress);
    }

    private VampirePowerLevel GetRequiredPowerLevel(EntProtoId actionId)
    {
        if (_prototype.TryIndex<EntityPrototype>(actionId, out var prototype) &&
            prototype.TryGetComponent<VampireActionComponent>(out var vampireAction, _componentFactory))
        {
            return vampireAction.RequiredPowerLevel;
        }

        return VampirePowerLevel.Neonate;
    }

    private readonly record struct VampireActionChargeState(
        int CurrentCharges,
        int MaxCharges,
        float RechargeProgress);
}

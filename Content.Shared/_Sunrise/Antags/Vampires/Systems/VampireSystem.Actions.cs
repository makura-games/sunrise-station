using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Robust.Shared.Player;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private ActionGrantSystem _actionGrant = default!;
    [Dependency] private EntityQuery<VampireActionComponent> _vampireActionQuery = default!;
    [Dependency] private EntityQuery<LimitedChargesComponent> _chargesQuery = default!;
    [Dependency] private EntityQuery<AutoRechargeComponent> _rechargeQuery = default!;

    [SubscribeLocalEvent(after: [typeof(ActionGrantSystem)])]
    public void OnMapInit(Entity<VampireComponent> ent, ref MapInitEvent args)
    {
        SyncVampireActions(ent);
    }

    [SubscribeLocalEvent]
    public void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (TryComp<VampireComponent>(ev.Entity, out var vampire))
            SyncVampireActions((ev.Entity, vampire));
    }

    private void SyncVampireActions(Entity<VampireComponent> ent)
    {
        if (LifeStage(ent) < EntityLifeStage.MapInitialized ||
            !_configurationQuery.TryComp(ent, out var configuration) ||
            !TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
        {
            return;
        }

        foreach (var action in _actions.GetActions(ent))
        {
            if (action.Comp.Container != ent.Owner ||
                !_vampireActionQuery.TryComp(action, out var vampireAction) ||
                Prototype(action) is not { } prototype)
            {
                continue;
            }

            _actions.SetEnabled(action.AsNullable(), ent.Comp.PowerLevel >= vampireAction.RequiredPowerLevel);
            VampireActionChargeSettings? settings = null;
            if (prototype.ID == configuration.FangsAction.Id)
                _actions.SetUseDelay(action.AsNullable(), configuration.FangsUseDelay);
            else if (prototype.ID == configuration.GlareAction.Id)
                settings = level.Glare.Action;
            else if (prototype.ID == configuration.RejuvenateAction.Id)
                settings = level.Rejuvenation.Action;
            else if (prototype.ID == configuration.SleepAction.Id)
            {
                settings = level.Sleep.Action;
                if (vampireAction.BloodCost != level.Sleep.BloodCost)
                {
                    vampireAction.BloodCost = level.Sleep.BloodCost;
                    DirtyField(action, vampireAction, nameof(VampireActionComponent.BloodCost));
                }

                _actions.SetRange((action.Owner, null), level.Sleep.TargetRange);
            }

            if (settings is null)
                continue;

            _actions.SetUseDelay(action.AsNullable(), settings.UseDelay);
            ConfigureActionCharges(action, settings);
        }
    }

    private void RemoveVampireActions(Entity<VampireComponent> ent)
    {
        foreach (var action in _actions.GetActions(ent).ToArray())
        {
            if (action.Comp.Container == ent.Owner && _vampireActionQuery.HasComp(action))
                _actions.RemoveAction((ent.Owner, null), action.AsNullable());
        }
    }

    private void ConfigureActionCharges(EntityUid action, VampireActionChargeSettings settings)
    {
        if (!_chargesQuery.TryComp(action, out var charges) ||
            !_rechargeQuery.TryComp(action, out var recharge) ||
            charges.MaxCharges == settings.MaxCharges && recharge.RechargeDuration == settings.RechargeDuration)
        {
            return;
        }

        var current = _charges.GetCurrentCharges((action, charges, recharge));
        var progress = 0f;
        if (current < charges.MaxCharges && recharge.RechargeDuration > TimeSpan.Zero)
        {
            var elapsedTicks = Math.Max(0L, (_timing.CurTime - charges.LastUpdate).Ticks % recharge.RechargeDuration.Ticks);
            progress = Math.Clamp((float)elapsedTicks / recharge.RechargeDuration.Ticks, 0f, 1f);
        }

        var addedCapacity = Math.Max(0, settings.MaxCharges - charges.MaxCharges);
        var newCharges = Math.Clamp(current + addedCapacity, 0, settings.MaxCharges);
        _charges.SetMaxCharges((action, charges), settings.MaxCharges);
        _charges.SetCharges((action, charges), newCharges);
        _charges.SetRechargeDuration((action, charges, recharge), settings.RechargeDuration,
            newCharges < settings.MaxCharges ? progress : 0f);
    }
}

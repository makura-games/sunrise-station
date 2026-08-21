using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Sunrise.EnergyShield;

public sealed class EnergyShieldSystem : EntitySystem
{
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EnergyShieldComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<EnergyShieldComponent, ItemToggleActivateAttemptEvent>(OnToggleAttempt);
        SubscribeLocalEvent<EnergyShieldComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
    }

    private void OnPowerCellSlotEmpty(Entity<EnergyShieldComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if (_itemToggle.IsActivated(ent.Owner))
            _itemToggle.TryDeactivate(ent.Owner);
    }

    private void OnDamage(Entity<EnergyShieldComponent> ent, ref DamageChangedEvent args)
    {
        if (!_itemToggle.IsActivated(ent.Owner))
            return;

        if (args.DamageDelta == null)
            return;

        if (!_powerCell.TryGetBatteryFromSlotOrEntity(ent.Owner, out var battery))
            return;

        var totalDamage = args.DamageDelta.GetTotal();
        if (totalDamage <= 0)
            return;

        var cost = totalDamage.Float() * ent.Comp.EnergyCostPerDamage;
        _battery.UseCharge(battery.Value.AsNullable(), cost);
        _audio.PlayPvs(ent.Comp.AbsorbSound, ent);

        if (_battery.GetCharge(battery.Value.AsNullable()) <= 0)
        {
            _itemToggle.TryDeactivate(ent.Owner);
            _audio.PlayPvs(ent.Comp.ShutdownSound, ent);
        }
    }

    private void OnToggleAttempt(Entity<EnergyShieldComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!_powerCell.TryGetBatteryFromSlotOrEntity(ent.Owner, out var battery))
        {
            if (Exists(args.User))
            {
                _popup.PopupEntity(
                    Loc.GetString("power-cell-no-battery"),
                    args.User.Value,
                    args.User.Value,
                    PopupType.Small
                );
            }
            args.Cancelled = true;
            return;
        }

        if (_battery.GetCharge(battery.Value.AsNullable()) >= battery.Value.Comp.MaxCharge * ent.Comp.MinChargeFractionForActivation)
        {
            return;
        }

        if (Exists(args.User))
        {
            _popup.PopupEntity(
                Loc.GetString("stunbaton-component-low-charge"),
                args.User.Value,
                args.User.Value,
                PopupType.Small
            );
        }

        args.Cancelled = true;
    }
}

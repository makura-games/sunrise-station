using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Sunrise.Biocode;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Timing;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Content.Shared.EnergyDome;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Robust.Server.Containers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Content.Shared.Damage.Systems;

namespace Content.Server.EnergyDome;

public sealed partial class EnergyDomeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private DeviceLinkSystem _signalSystem = default!;
    [Dependency] private ContainerSystem _containerSystem = default!;
    [Dependency] private BiocodeSystem _biocodeSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        //Generator events
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, MapInitEvent>(OnInit);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ActivateInWorldEvent>(OnActivatedInWorld);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ToggleActionEvent>(OnToggleAction);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ChargeChangedEvent>(OnChargeChanged);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, EntParentChangedMessage>(OnParentChanged);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, GetVerbsEvent<ActivationVerb>>(AddToggleDomeVerb);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ExaminedEvent>(OnExamine);


        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ComponentRemove>(OnComponentRemove);

        SubscribeLocalEvent<EnergyDomeComponent, DamageChangedEvent>(OnDomeDamaged);
        SubscribeLocalEvent<EnergyDomeProtectedUserComponent, EntParentChangedMessage>(OnProtectedEntityParentChanged);
    }

    private void OnInit(Entity<EnergyDomeGeneratorComponent> generator, ref MapInitEvent args)
    {
        if (generator.Comp.CanDeviceNetworkUse)
            _signalSystem.EnsureSinkPorts(generator, generator.Comp.TogglePort, generator.Comp.OnPort, generator.Comp.OffPort);
    }

    //different ways of use

    private void OnSignalReceived(Entity<EnergyDomeGeneratorComponent> generator, ref SignalReceivedEvent args)
    {
        if (!generator.Comp.CanDeviceNetworkUse)
            return;

        if (args.Port == generator.Comp.OnPort)
        {
            AttemptToggle(generator, true);
        }
        if (args.Port == generator.Comp.OffPort)
        {
            AttemptToggle(generator, false);
        }
        if (args.Port == generator.Comp.TogglePort)
        {
            AttemptToggle(generator, !generator.Comp.Enabled);
        }
    }

    private void OnAfterInteract(Entity<EnergyDomeGeneratorComponent> generator, ref AfterInteractEvent args)
    {
        if (generator.Comp.CanInteractUse)
            AttemptToggle(generator, !generator.Comp.Enabled);
    }

    private void OnActivatedInWorld(Entity<EnergyDomeGeneratorComponent> generator, ref ActivateInWorldEvent args)
    {
        // Sunrise-Start
        if (!_containerSystem.ContainsEntity(args.User, generator.Owner))
            return;

        if (TryComp<BiocodeComponent>(generator.Owner, out var biocodedComponent))
        {
            if (!_biocodeSystem.CanUse(args.User, biocodedComponent.Factions))
                return;
        }
        // Sunrise-End

        if (generator.Comp.CanInteractUse)
            AttemptToggle(generator, !generator.Comp.Enabled);
    }

    private void OnExamine(Entity<EnergyDomeGeneratorComponent> generator, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(
            (generator.Comp.Enabled)
            ? "energy-dome-on-examine-is-on-message"
            : "energy-dome-on-examine-is-off-message"
            ));
    }

    private void AddToggleDomeVerb(Entity<EnergyDomeGeneratorComponent> generator, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !generator.Comp.CanInteractUse)
            return;

        // Sunrise-Start
        if (!_containerSystem.ContainsEntity(args.User, generator.Owner))
            return;

        if (TryComp<BiocodeComponent>(generator.Owner, out var biocodedComponent))
        {
            if (!_biocodeSystem.CanUse(args.User, biocodedComponent.Factions))
                return;
        }
        // Sunrise-End

        var @event = args;
        ActivationVerb verb = new()
        {
            Text = Loc.GetString("energy-dome-verb-toggle"),
            Act = () => AttemptToggle(generator, !generator.Comp.Enabled)
        };

        args.Verbs.Add(verb);
    }
    private void OnGetActions(Entity<EnergyDomeGeneratorComponent> generator, ref GetItemActionsEvent args)
    {
        if (generator.Comp.CanInteractUse)
            args.AddAction(ref generator.Comp.ToggleActionEntity, generator.Comp.ToggleAction);
    }

    private void OnToggleAction(Entity<EnergyDomeGeneratorComponent> generator, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_containerSystem.ContainsEntity(args.Performer, generator.Owner))
            return;

        if (TryComp<BiocodeComponent>(generator.Owner, out var biocodedComponent))
        {
            if (!_biocodeSystem.CanUse(args.Performer, biocodedComponent.Factions))
                return;
        }

        AttemptToggle(generator, !generator.Comp.Enabled);

        args.Handled = true;
    }

    // System interactions

    private void OnPowerCellSlotEmpty(Entity<EnergyDomeGeneratorComponent> generator, ref PowerCellSlotEmptyEvent args)
    {
        TurnOff(generator, true);
    }

    private void OnPowerCellChanged(Entity<EnergyDomeGeneratorComponent> generator, ref PowerCellChangedEvent args)
    {
        if (args.Ejected || !_powerCell.HasDrawCharge(generator.Owner))
            TurnOff(generator, true);
    }

    private void OnChargeChanged(Entity<EnergyDomeGeneratorComponent> generator, ref ChargeChangedEvent args)
    {
        if (args.CurrentCharge == 0)
            TurnOff(generator, true);
    }
    private void OnDomeDamaged(Entity<EnergyDomeComponent> dome, ref DamageChangedEvent args)
    {
        if (dome.Comp.Generator == null)
            return;

        var generatorUid = dome.Comp.Generator.Value;

        if (!TryComp<EnergyDomeGeneratorComponent>(generatorUid, out var generatorComp))
            return;

        if (args.DamageDelta == null)
            return;

        float totalDamage = args.DamageDelta.GetTotal().Float();
        var energyLeak = totalDamage * generatorComp.DamageEnergyDraw;

        _audio.PlayPvs(generatorComp.ParrySound, dome);

        if (HasComp<PowerCellDrawComponent>(generatorUid))
        {
            _powerCell.TryGetBatteryFromSlot(generatorUid, out var cell);
            if (cell != null)
            {
                _battery.UseCharge(cell.Value.Owner, energyLeak);

                if (cell.Value.Comp.State == BatteryState.Empty)
                    TurnOff((generatorUid, generatorComp), true);
            }
        }

        //it seems to me it would not work well to hang both a powercell and an internal battery with wire charging on the object....
        if (TryComp<BatteryComponent>(generatorUid, out var battery)) {
            _battery.UseCharge(generatorUid, energyLeak);

            if (battery.State == BatteryState.Empty)
                TurnOff((generatorUid, generatorComp), true);
        }
    }

    private void OnParentChanged(Entity<EnergyDomeGeneratorComponent> generator, ref EntParentChangedMessage args)
    {
        //To do: taking the active barrier in hand for some reason does not manage to change the parent in this case,
        //and the barrier is not turned off.
        //
        //Laying down works well (-_-)
        if (GetProtectedEntity(generator) != generator.Comp.DomeParentEntity)
            TurnOff(generator, false);
    }

    private void OnComponentRemove(Entity<EnergyDomeGeneratorComponent> generator, ref ComponentRemove args)
    {
        TurnOff(generator, false);
    }

    // Functional

    public bool AttemptToggle(Entity<EnergyDomeGeneratorComponent> generator, bool status)
    {
        var parent = Transform(generator.Owner).ParentUid;

        if (HasComp<ContainerManagerComponent>(Transform(parent).ParentUid))
            return false;

        if (TryComp<UseDelayComponent>(generator, out var useDelay) &&
            _useDelay.IsDelayed((generator, useDelay)))
        {
            Fail(generator, "energy-dome-recharging");
            return false;
        }

        if (TryComp<PowerCellSlotComponent>(generator, out _))
        {
            if (!_powerCell.TryGetBatteryFromSlot(generator.Owner, out _))
            {
                Fail(generator, "energy-dome-no-cell");
                return false;
            }

            if (!_powerCell.HasDrawCharge(generator.Owner))
            {
                Fail(generator, "energy-dome-no-power");
                return false;
            }
        }
        else if (TryComp<BatteryComponent>(generator, out var battery))
        {
            if (_battery.GetCharge((generator, battery)) <= 0)
            {
                Fail(generator, "energy-dome-no-power");
                return false;
            }
        }
        else
        {
            Fail(generator, "energy-dome-no-power");
            return false;
        }

        Toggle(generator, status);
        return true;
    }
    private void Fail(Entity<EnergyDomeGeneratorComponent> generator, string locKey)
    {
        _audio.PlayPvs(generator.Comp.TurnOffSound, generator);
        _popup.PopupEntity(Loc.GetString(locKey), generator);
    }
    private void Toggle(Entity<EnergyDomeGeneratorComponent> generator, bool status)
    {
        if (status)
            TurnOn(generator);
        else
            TurnOff(generator, false);
    }

    private void TurnOn(Entity<EnergyDomeGeneratorComponent> generator)
    {
        if (generator.Comp.Enabled)
            return;

        var protectedEntity = GetProtectedEntity(generator);

        var newDome = Spawn(generator.Comp.DomePrototype, Transform(protectedEntity).Coordinates);
        generator.Comp.DomeParentEntity = protectedEntity;
        _transform.SetParent(newDome, protectedEntity);

        if (TryComp<EnergyDomeComponent>(newDome, out var domeComp))
        {
            domeComp.Generator = generator;
        }

        var protectedComp = EnsureComp<EnergyDomeProtectedUserComponent>(protectedEntity);
        protectedComp.DomeEntity = newDome;

        if (TryComp<PowerCellDrawComponent>(generator.Owner, out var powerCellDrawComponent))
        {
            _powerCell.SetDrawEnabled(generator.Owner, true);
        }

        generator.Comp.SpawnedDome = newDome;
        _audio.PlayPvs(generator.Comp.TurnOnSound, generator);
        generator.Comp.Enabled = true;
    }

    // Sunrise: обработчик смены парента защищаемой сущности
    private void OnProtectedEntityParentChanged(Entity<EnergyDomeProtectedUserComponent> ent, ref EntParentChangedMessage args)
    {
        if (ent.Comp.DomeEntity == null)
            return;
        if (HasComp<ContainerManagerComponent>(Transform(ent).ParentUid))
        {
            if (TryComp<EnergyDomeComponent>(ent.Comp.DomeEntity.Value, out var domeComp) && domeComp.Generator != null)
            {
                if (TryComp<EnergyDomeGeneratorComponent>(domeComp.Generator.Value, out var genComp))
                {
                    TurnOff((domeComp.Generator.Value, genComp), false);
                }
            }
        }
    }

    private void TurnOff(Entity<EnergyDomeGeneratorComponent> generator, bool startReloading)
    {
        if (!generator.Comp.Enabled)
            return;

        generator.Comp.Enabled = false;
        QueueDel(generator.Comp.SpawnedDome);

        if (generator.Comp.DomeParentEntity != null && HasComp<EnergyDomeProtectedUserComponent>(generator.Comp.DomeParentEntity.Value))
        {
            RemCompDeferred<EnergyDomeProtectedUserComponent>(generator.Comp.DomeParentEntity.Value);
        }

        if (TryComp<PowerCellDrawComponent>(generator.Owner, out var powerCellDrawComponent))
        {
            _powerCell.SetDrawEnabled(generator.Owner, false);
        }

        _audio.PlayPvs(generator.Comp.TurnOffSound, generator);
        if (startReloading)
        {
            _audio.PlayPvs(generator.Comp.EnergyOutSound, generator);
            if (TryComp<UseDelayComponent>(generator, out var useDelay))
            {
                _useDelay.TryResetDelay(new Entity<UseDelayComponent>(generator, useDelay));
            }
        }
    }

    // Util

    private EntityUid GetProtectedEntity(EntityUid entity)
    {
        return (_container.TryGetOuterContainer(entity, Transform(entity), out var container))
            ? container.Owner
            : entity;
    }
}

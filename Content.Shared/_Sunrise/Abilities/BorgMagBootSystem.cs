using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Atmos.Components;
using Content.Shared.Gravity;
using Content.Shared.Movement.Systems;
using Content.Shared.Slippery;

namespace Content.Shared._Sunrise.Abilities;

public sealed partial class SharedBorgMagbootsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _sharedActions = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifierSystem = default!;
    [Dependency] private AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgMagbootsComponent, SlipAttemptEvent>(OnSlipAttempt);
        SubscribeLocalEvent<BorgMagbootsComponent, ToggleBorgMagbootsActionEvent>(OnToggleAction);
        SubscribeLocalEvent<BorgMagbootsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<BorgMagbootsComponent, IsWeightlessEvent>(OnIsWeightless);
        SubscribeLocalEvent<BorgMagbootsComponent, MapInitEvent>(OnInit);
    }

    private void OnInit(EntityUid uid, BorgMagbootsComponent component, MapInitEvent args)
    {
        _sharedActions.AddAction(uid, ref component.ToggleActionEntity, component.ToggleAction);
    }

    private void OnRefreshMovementSpeedModifiers(EntityUid uid, BorgMagbootsComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        var walkMod = 1f;
        var sprintMod = 1f;
        if (component.On)
        {
            walkMod = component.WalkModifier;
            sprintMod = component.SprintModifier;
        }

        args.ModifySpeed(walkMod, sprintMod);
    }

    private void OnToggleAction(Entity<BorgMagbootsComponent> ent, ref ToggleBorgMagbootsActionEvent args)
    {
        if (args.Handled)
            return;

        ToggleMagboots(ent);

        args.Handled = true;
    }

    private void ToggleMagboots(Entity<BorgMagbootsComponent> ent)
    {
        ent.Comp.On = !ent.Comp.On;

        UpdateMagbootEffects(ent.Owner, ent, ent.Comp.On);
        _sharedActions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.On);
        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(ent.Owner);
        Dirty(ent);
    }

    public void UpdateMagbootEffects(EntityUid user, Entity<BorgMagbootsComponent> ent, bool state)
    {
        // TODO: public api for this and add access
        if (TryComp<MovedByPressureComponent>(user, out var moved))
            moved.Enabled = !state;

        _gravity.RefreshWeightless(user);

        if (state)
            _alerts.ShowAlert(user, ent.Comp.MagbootsAlert);
        else
            _alerts.ClearAlert(user, ent.Comp.MagbootsAlert);
    }

    private void OnIsWeightless(Entity<BorgMagbootsComponent> ent, ref IsWeightlessEvent args)
    {
        if (args.Handled || !ent.Comp.On)
        {
            return;
        }

        // do not cancel weightlessness if the person is in off-grid.
        if (ent.Comp.RequiresGrid && !_gravity.EntityOnGravitySupportingGridOrMap(ent.Owner))
            return;

        args.IsWeightless = false;
        args.Handled = true;
    }

    private void OnSlipAttempt(EntityUid uid, BorgMagbootsComponent component, SlipAttemptEvent args)
    {
        if (component.On)
            args.NoSlip = true;
    }
}

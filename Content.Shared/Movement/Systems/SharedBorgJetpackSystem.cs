using Content.Shared.Actions;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Gravity;
using Content.Shared.Popups;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Movement.Systems;

public sealed partial class SharedBorgJetpackSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<BorgJetpackComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BorgJetpackComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BorgJetpackComponent, ToggleJetpackEvent>(OnJetpackToggle);
        SubscribeLocalEvent<BorgJetpackUserComponent, RefreshWeightlessModifiersEvent>(OnJetpackUserWeightlessMovement);
        SubscribeLocalEvent<BorgJetpackUserComponent, CanWeightlessMoveEvent>(OnJetpackUserCanWeightless);
        SubscribeLocalEvent<BorgJetpackUserComponent, EntParentChangedMessage>(OnJetpackUserEntParentChanged);
        SubscribeLocalEvent<GravityChangedEvent>(OnJetpackUserGravityChanged);
    }

    private void OnStartup(EntityUid uid, BorgJetpackComponent component, ComponentStartup args)
    {
        _actionContainer.EnsureAction(uid, ref component.ToggleActionEntity, component.ToggleAction);
        
        if (component.ToggleActionEntity != null)
            _actions.AddAction(uid, component.ToggleActionEntity.Value, uid);
    }

    private void OnShutdown(EntityUid uid, BorgJetpackComponent component, ComponentShutdown args)
    {
        if (IsEnabled(uid))
            SetEnabled(uid, component, false);

        if (component.ToggleActionEntity != null)
            _actions.RemoveAction(uid, component.ToggleActionEntity.Value);
    }

    private void OnJetpackToggle(EntityUid uid, BorgJetpackComponent component, ToggleJetpackEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp(uid, out TransformComponent? xform) && !CanEnableOnGrid(xform.GridUid))
        {
            _popup.PopupClient(Loc.GetString("jetpack-no-station"), uid, args.Performer);
            return;
        }

        var enabled = !IsEnabled(uid);
        SetEnabled(uid, component, enabled);

        if (component.ToggleActionEntity != null)
            _actions.SetToggled(component.ToggleActionEntity.Value, enabled);

        args.Handled = true;
    }

    private void OnJetpackUserWeightlessMovement(EntityUid uid, BorgJetpackUserComponent component, ref RefreshWeightlessModifiersEvent args)
    {
        if (!TryComp<BorgJetpackComponent>(component.Jetpack, out var jetpack))
            return;

        args.WeightlessAcceleration = jetpack.Acceleration;
        args.WeightlessModifier = jetpack.WeightlessModifier;
        args.WeightlessFriction = jetpack.Friction;
        args.WeightlessFrictionNoInput = jetpack.Friction;
    }

    private void OnJetpackUserCanWeightless(EntityUid uid, BorgJetpackUserComponent component, ref CanWeightlessMoveEvent args)
    {
        args.CanMove = true;
    }

    private void OnJetpackUserEntParentChanged(EntityUid uid, BorgJetpackUserComponent component, ref EntParentChangedMessage args)
    {
        if (TryComp<BorgJetpackComponent>(component.Jetpack, out var jetpack) &&
            !CanEnableOnGrid(args.Transform.GridUid))
        {
            DisableJetpack(component.Jetpack.Value, jetpack, uid);
        }
    }

    private void OnJetpackUserGravityChanged(ref GravityChangedEvent ev)
    {
        var gridUid = ev.ChangedGridIndex;
        var jetpackQuery = GetEntityQuery<BorgJetpackComponent>();

        var query = EntityQueryEnumerator<BorgJetpackUserComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var user, out var transform))
        {
            if (transform.GridUid == gridUid && ev.HasGravity &&
                jetpackQuery.TryGetComponent(user.Jetpack, out var jetpack))
            {
                DisableJetpack(user.Jetpack.Value, jetpack, uid);
            }
        }
    }

    private void DisableJetpack(EntityUid jetpackUid, BorgJetpackComponent component, EntityUid userUid)
    {
        SetEnabled(jetpackUid, component, false);
        _popup.PopupClient(Loc.GetString("jetpack-to-grid"), userUid, userUid);
    }

    private bool CanEnableOnGrid(EntityUid? gridUid)
    {
        return gridUid == null ||
               TryComp<GravityComponent>(gridUid, out var gravity) &&
               !gravity.Enabled;
    }

    private bool IsEnabled(EntityUid uid)
    {
        return HasComp<ActiveJetpackComponent>(uid);
    }

    public void SetEnabled(EntityUid uid, BorgJetpackComponent component, bool enabled)
    {
        if (IsEnabled(uid) == enabled)
            return;

        if (enabled)
        {
            SetupUser(uid, component);
            EnsureComp<ActiveJetpackComponent>(uid);
        }
        else
        {
            RemoveUser(uid, component);
            RemComp<ActiveJetpackComponent>(uid);
        }

        _appearance.SetData(uid, JetpackVisuals.Enabled, enabled);
    }

    private void SetupUser(EntityUid uid, BorgJetpackComponent component)
    {
        var user = uid;
        component.JetpackUser = user;

        var userComp = EnsureComp<BorgJetpackUserComponent>(user);
        userComp.Jetpack = uid;
        userComp.WeightlessAcceleration = component.Acceleration;
        userComp.WeightlessModifier = component.WeightlessModifier;
        userComp.WeightlessFriction = component.Friction;
        userComp.WeightlessFrictionNoInput = component.Friction;

        if (TryComp<PhysicsComponent>(user, out var physics))
            _physics.SetBodyStatus(user, physics, BodyStatus.InAir);

        _movementSpeedModifier.RefreshWeightlessModifiers(user);
    }

    private void RemoveUser(EntityUid uid, BorgJetpackComponent component)
    {
        if (component.JetpackUser == null || !RemComp<BorgJetpackUserComponent>(component.JetpackUser.Value))
            return;

        if (TryComp<PhysicsComponent>(component.JetpackUser.Value, out var physics))
            _physics.SetBodyStatus(component.JetpackUser.Value, physics, BodyStatus.OnGround);

        _movementSpeedModifier.RefreshWeightlessModifiers(component.JetpackUser.Value);
        component.JetpackUser = null;
    }
}

[RegisterComponent]
public sealed partial class BorgJetpackUserComponent : Component
{
    [ViewVariables]
    public EntityUid? Jetpack;

    [ViewVariables(VVAccess.ReadWrite)]
    public float WeightlessAcceleration;

    [ViewVariables(VVAccess.ReadWrite)]
    public float WeightlessModifier;

    [ViewVariables(VVAccess.ReadWrite)]
    public float WeightlessFriction;

    [ViewVariables(VVAccess.ReadWrite)]
    public float WeightlessFrictionNoInput;
}

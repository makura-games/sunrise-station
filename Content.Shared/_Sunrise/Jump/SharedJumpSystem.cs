using Content.Shared._Sunrise.Animations;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Climbing.Systems;
using Content.Shared.Emoting;
using Content.Shared.Gravity;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Content.Shared.Movement.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Damage.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Damage.Components;
using Content.Shared.Ghost;
using Content.Shared.Buckle;

namespace Content.Shared._Sunrise.Jump;

public abstract partial class SharedJumpSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StandingStateSystem _standingState = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedStaminaSystem _staminaSystem = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;


    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery = default!;

    private static readonly EntProtoId JumpStatusEffectKey = "StatusEffectJump";
    private static readonly ProtoId<EmotePrototype> EmoteJumpProto = "Jump";

    private static readonly SoundSpecifier JumpSound = new SoundPathSpecifier("/Audio/_Sunrise/jump_mario.ogg");

    private readonly List<ICommonSession> _ignoredRecipients = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JumpStatusEffectComponent, StatusEffectAppliedEvent>(OnStartup);
        SubscribeLocalEvent<JumpStatusEffectComponent, StatusEffectRemovedEvent>(OnShutdown);
        SubscribeNetworkEvent<ClientOptionDisableJumpSoundEvent>(OnClientOptionJumpSound);
        SubscribeLocalEvent<BunnyHopComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<EmoteAnimationComponent, BeforeEmoteEvent>(CheckEmote);
        SubscribeLocalEvent<JumpStatusEffectComponent, StatusEffectRelayedEvent<TryStandDoAfterEvent>>(OnStandAttempt);

        _cfg.OnValueChanged(SunriseCCVars.SunriseCCVars.JumpEnable, OnJumpEnableChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.SunriseCCVars.BunnyHopEnable, OnBunnyHopEnableChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.SunriseCCVars.BunnyHopMinSpeedThreshold, OnBunnyHopMinSpeedThresholdChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.SunriseCCVars.BunnyHopSpeedBoostWindow, OnBunnyHopSpeedBoostWindowChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.SunriseCCVars.BunnyHopSpeedUpPerJump, OnBunnyHopSpeedUpPerJumpChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.SunriseCCVars.BunnyHopSpeedLimit, OnBunnyHopSpeedLimitChanged, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_bunnyHopEnabled)
            return;

        var query = EntityQueryEnumerator<BunnyHopComponent, PhysicsComponent>();

        while (query.MoveNext(out var uid, out var bunnyHop, out var physics))
        {
            var timeSinceLastLand = _timing.CurTime - bunnyHop.LastLandingTime;
            var currentSpeed = physics.LinearVelocity.Length();

            if (timeSinceLastLand > _bunnyHopSpeedBoostWindow || currentSpeed < _bunnyHopMinSpeedThreshold)
            {
                RemComp<BunnyHopComponent>(uid);
                _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
            }
        }
    }
    private void CheckEmote(EntityUid uid, EmoteAnimationComponent component, BeforeEmoteEvent args)
    {
        if (args.Emote != EmoteJumpProto)
            return;

        if (!HasComp<CanJumpComponent>(uid))
            return;

        if (!CanJump(uid))
            args.Cancel();
    }

    private static void OnRefreshMoveSpeed(Entity<BunnyHopComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.CanBunnyHop)
            args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }
    private void OnStartup(Entity<JumpStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp<CanJumpComponent>(args.Target, out var jump))
            return;

        if (jump.IsOnlyEmotion)
            return;

        if (!TryComp<StaminaComponent>(args.Target, out var stamina))
            return;

        if (!_physicsQuery.TryGetComponent(args.Target, out var body) ||
            !_fixturesQuery.TryGetComponent(args.Target, out var fixtures))
            return;

        if (_net.IsServer)
        {
            _audio.PlayEntity(JumpSound, Filter.Pvs(args.Target).RemovePlayers(_ignoredRecipients),
                args.Target, true, AudioParams.Default.WithVolume(-5f));

            _staminaSystem.TakeStaminaDamage(args.Target, jump.StaminaDamage * stamina.CritThreshold, null, args.Target, args.Target, ignoreResist: true);
        }

        EnsureComp<CanMoveInAirComponent>(args.Target);
        _physics.SetBodyStatus(args.Target, body, BodyStatus.InAir);
        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            jump.OriginalCollisionMasks[id] = fixture.CollisionMask;
            jump.OriginalCollisionLayers[id] = fixture.CollisionLayer;

            _physics.RemoveCollisionMask(args.Target, id, fixture, (int)CollisionGroup.MidImpassable, manager: fixtures);
        }
        // There is a problem with synchronization speed reduction due to stamina with increased bhop speed.
        // This leads to lag.
        // TryBunnyHop(args.Target, body);
    }
    private void OnShutdown(Entity<JumpStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp<CanJumpComponent>(args.Target, out var jump))
            return;

        if (jump.IsOnlyEmotion)
            return;

        if (!_physicsQuery.TryComp(args.Target, out var body)
            || !_fixturesQuery.TryComp(args.Target, out var fixtures))
            return;

        RemCompDeferred<CanMoveInAirComponent>(args.Target);
        _physics.SetBodyStatus(args.Target, body, BodyStatus.OnGround);

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (jump.OriginalCollisionMasks.TryGetValue(id, out var originalMask))
            {
                _physics.SetCollisionMask(args.Target, id, fixture, originalMask, manager: fixtures);
            }
            if (jump.OriginalCollisionLayers.TryGetValue(id, out var originalLayer))
            {
                _physics.SetCollisionLayer(args.Target, id, fixture, originalLayer, manager: fixtures);
            }
        }

        if (TryComp<BunnyHopComponent>(args.Target, out var bunnyHopComp))
            bunnyHopComp.LastLandingTime = _timing.CurTime;
    }
    private void OnStandAttempt(Entity<JumpStatusEffectComponent> ent, ref StatusEffectRelayedEvent<TryStandDoAfterEvent> args)
    {
        args.Args.Handled = true;
    }
    public bool CanJump(EntityUid uid)
    {
        if (!TryComp<CanJumpComponent>(uid, out var jump))
            return false;

        if (!_enabled)
            return false;

        if (_gravity.IsWeightless(uid))
            return false;

        if (_standingState.IsDown(uid))
            return false;

        if (!_mobState.IsAlive(uid))
            return false;

        if (HasComp<ThrownItemComponent>(uid))
            return false;

        if (_climb.IsClimbing(uid))
            return false;

        if (_buckle.IsBuckled(uid))
            return false;

        if (jump.IsOnlyEmotion)
            return true;

        if (!TryComp<StaminaComponent>(uid, out var stamina))
            return false;

        var threshold = stamina.CritThreshold * (1 - jump.MinimumStamina);

        if (stamina.StaminaDamage >= threshold)
            return false;

        return true;
    }

    public bool TryJump(EntityUid uid)
    {
        if (!CanJump(uid))
            return false;

        Jump(uid);
        return true;
    }

    public void Jump(EntityUid uid)
    {
        if (!TryComp<CanJumpComponent>(uid, out var jump))
            return;
        _statusEffects.TryAddStatusEffectDuration(uid,
            JumpStatusEffectKey,
            jump.JumpInAirTime);
    }

    private bool TryBunnyHop(Entity<JumpStatusEffectComponent> ent, PhysicsComponent body)
    {
        if (!_bunnyHopEnabled)
            return false;

        if (TryComp<PullerComponent>(ent, out var pull) && _pulling.IsPulling(ent, pull))
            return false;

        var currentSpeed = body.LinearVelocity.Length();

        if (currentSpeed < _bunnyHopMinSpeedThreshold)
            return false;

        var bunnyHopComp = EnsureComp<BunnyHopComponent>(ent);
        bunnyHopComp.LastLandingTime = _timing.CurTime;

        var timeSinceLastLand = _timing.CurTime - bunnyHopComp.LastLandingTime;
        if (timeSinceLastLand <= _bunnyHopSpeedBoostWindow)
        {
            var speedMultiplier = bunnyHopComp.SpeedMultiplier += _bunnyHopSpeedUpPerJump;
            bunnyHopComp.SpeedMultiplier = Math.Min(speedMultiplier, _bunnyHopSpeedLimit);

            _movementSpeedModifier.RefreshMovementSpeedModifiers(ent);
        }

        return true;
    }
}

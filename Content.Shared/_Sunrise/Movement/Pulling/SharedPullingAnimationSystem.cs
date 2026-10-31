using System.Numerics;
using Content.Shared.Coordinates;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Sunrise.Movement.Pulling;

public sealed partial class SharedPullingAnimationSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PullableComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<PullableComponent, PullStoppedMessage>(OnPullStopped);
        SubscribeLocalEvent<ActivePullingAnimationComponent, ComponentStartup>(OnAnimationStartup);
        SubscribeLocalEvent<ActivePullingAnimationComponent, ComponentShutdown>(OnAnimationShutdown);
    }

    private void OnPullStarted(Entity<PullableComponent> ent, ref PullStartedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        TryPlayPullAnimation(args.PullerUid, args.PulledUid);
    }

    private void OnPullStopped(Entity<PullableComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        TryStopPullAnimation(args.PullerUid, args.PulledUid);
    }

    public bool TryPlayPullAnimation(EntityUid puller, EntityUid pulled)
    {
        if (!CanPlayPullAnimation(puller, pulled))
            return false;

        var active = EnsureComp<ActivePullingAnimationComponent>(pulled);
        DoPullLunge(puller, pulled, active.PullSound);
        return true;
    }

    public bool TryStopPullAnimation(EntityUid puller, EntityUid pulled)
    {
        if (!CanStopPullAnimation(pulled))
            return false;

        RemComp<ActivePullingAnimationComponent>(pulled);

        if (Exists(puller))
            DoPullLunge(puller, pulled, null);

        return true;
    }

    public bool CanPlayPullAnimation(EntityUid puller, EntityUid pulled)
    {
        return Exists(puller) && Exists(pulled);
    }

    public bool CanStopPullAnimation(EntityUid pulled)
    {
        return Exists(pulled);
    }

    private void DoPullLunge(EntityUid puller, EntityUid pulled, SoundSpecifier? sound)
    {
        var localPos = GetPullLocalPosition(puller, pulled);
        _melee.DoLunge(puller, puller, Angle.Zero, localPos, null);

        if (sound != null)
            _audio.PlayPredicted(sound, pulled, puller);
    }

    private void OnAnimationStartup(Entity<ActivePullingAnimationComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Effect = PredictedSpawnAttachedTo(ent.Comp.EffectPrototype, ent.Owner.ToCoordinates());
        Dirty(ent);
    }

    private void OnAnimationShutdown(Entity<ActivePullingAnimationComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Effect is not { } effect)
            return;

        PredictedQueueDel(effect);
    }

    private Vector2 GetPullLocalPosition(EntityUid puller, EntityUid pulled)
    {
        var pullerXform = Transform(puller);
        var targetPos = _transform.GetWorldPosition(pulled);
        // Переводим мировую позицию цели в локальные координаты тянущего для корректного направления рывка.
        var localPos = Vector2.Transform(targetPos, _transform.GetInvWorldMatrix(pullerXform));
        return pullerXform.LocalRotation.RotateVec(localPos);
    }
}

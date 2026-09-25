using Content.Shared._Sunrise.Movement.Carrying;
using Content.Shared.Rotation;
using Content.Shared.Stunnable;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Animations;

/// <summary>
/// transitions between base and override poses without absorbing rotation from other animation keys
/// PVS reset invalidates the applied pose so the current target can be restored on re-entry
/// </summary>
public sealed partial class SpritePoseSystem : EntitySystem
{
    [Dependency] private SpriteAnimationSystem _animation = default!;

    public const string AnimationKey = "sprite-pose";

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(AppearanceSystem));
        UpdatesBefore.Add(typeof(SpriteAnimationSystem));
        base.Initialize();
        SubscribeLocalEvent<CrawlerComponent, ComponentStartup>(OnCrawlerStartup);
        SubscribeLocalEvent<RotationVisualsComponent, ComponentRemove>(OnRotationRemove);
        SubscribeLocalEvent<CrawlerComponent, ComponentRemove>(OnCrawlerRemove);
        SubscribeLocalEvent<SpritePoseComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SpritePoseComponent, SpriteAnimationResetEvent>(OnReset);
    }

    private void OnCrawlerStartup(Entity<CrawlerComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<SpritePoseComponent>(ent);
    }

    private void OnRotationRemove(Entity<RotationVisualsComponent> ent, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(ent) && !HasComp<CrawlerComponent>(ent))
            RemComp<SpritePoseComponent>(ent);
    }

    private void OnCrawlerRemove(Entity<CrawlerComponent> ent, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(ent) && !HasComp<RotationVisualsComponent>(ent))
            RemComp<SpritePoseComponent>(ent);
    }

    private void OnShutdown(Entity<SpritePoseComponent> ent, ref ComponentShutdown args)
    {
        _animation.Stop(ent, AnimationKey);
    }

    private void OnReset(Entity<SpritePoseComponent> ent, ref SpriteAnimationResetEvent args)
    {
        ent.Comp.HasPose = false;
    }

    /// <summary>
    /// stores the base pose and applies it unless an override is active
    /// duration is in seconds; non-positive values apply the target immediately
    /// </summary>
    public void SetBasePose(Entity<SpriteComponent> ent, Angle rotation, float duration)
    {
        var pose = EnsureComp<SpritePoseComponent>(ent);
        pose.BaseRotation = rotation;

        if (!pose.OverrideRotation.HasValue)
            Animate(ent, rotation, duration, pose);
    }

    /// <summary>
    /// applies an override pose while keeping the base pose for later restoration
    /// duration is in seconds; an unchanged target preserves the current transition
    /// </summary>
    public void SetOverride(Entity<SpriteComponent> ent, Angle rotation, float duration)
    {
        var pose = EnsureComp<SpritePoseComponent>(ent);
        pose.OverrideRotation = rotation;
        Animate(ent, rotation, duration, pose);
    }

    /// <summary>
    /// removes an active override and returns to the stored base pose over duration seconds
    /// does nothing without an override; non-positive durations restore the base immediately
    /// </summary>
    public void ClearOverride(Entity<SpriteComponent> ent, float duration)
    {
        if (!TryComp<SpritePoseComponent>(ent, out var pose) || !pose.OverrideRotation.HasValue)
            return;

        pose.OverrideRotation = null;
        Animate(ent, pose.BaseRotation, duration, pose);
    }

    private void Animate(Entity<SpriteComponent> ent, Angle rotation, float duration, SpritePoseComponent pose)
    {
        var baseRotation = _animation.GetBaseRotation(ent);
        if (pose.HasPose && pose.Rotation.Equals(rotation) && baseRotation.Equals(rotation))
            return;

        var from = baseRotation + _animation.GetRotationOffset(ent, AnimationKey);
        _animation.Stop(ent, AnimationKey);
        _animation.SetBaseRotation(ent, rotation);
        if (duration > 0f)
            _animation.PlayRotation(ent, AnimationKey, (from - rotation, 0f), (Angle.Zero, duration));

        pose.Rotation = rotation;
        pose.HasPose = true;
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<SpritePoseComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var pose, out var sprite))
        {
            var meta = MetaData(uid);
            if ((meta.Flags & MetaDataFlags.Detached) != 0 || HasComp<ActiveCanBeCarriedComponent>(uid))
            {
                pose.HasPose = false;
                _animation.Stop(uid, AnimationKey);
                continue;
            }

            if (meta.EntityPaused)
                continue;

            var target = pose.OverrideRotation ?? pose.BaseRotation;
            if (!pose.HasPose || !pose.Rotation.Equals(target))
                Animate((uid, sprite), target, 0f, pose);
        }
    }
}

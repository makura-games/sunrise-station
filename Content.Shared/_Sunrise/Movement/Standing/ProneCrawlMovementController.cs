using System.Numerics;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics.Controllers;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Movement.Standing;

public sealed partial class ProneCrawlMovementController : VirtualController
{
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private IGameTiming _timing = default!;

    [Dependency] private EntityQuery<PullableComponent> _pullableQuery = default!;
    [Dependency] private EntityQuery<StandingStateComponent> _standingQuery = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SharedMoverController));
        UpdatesBefore.Add(typeof(SharedConveyorController));
        base.Initialize();
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<ActiveProneCrawlMovementComponent, CrawlerComponent, InputMoverComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var movement, out var crawler, out var mover, out var body))
        {
            if (prediction && !body.Predict)
                continue;

            UpdateCrawl((uid, movement, crawler, mover, body));
        }
    }

    private void UpdateCrawl(Entity<ActiveProneCrawlMovementComponent, CrawlerComponent, InputMoverComponent, PhysicsComponent> ent)
    {
        if (!CanCrawl(ent, out var wishDir))
        {
            Stop(ent, true, false);
            return;
        }

        var now = _timing.CurTime;
        if (ent.Comp1.Pulling)
        {
            if (now < ent.Comp1.PullEnd)
            {
                SetVelocity(ent, ent.Comp1.Velocity);
                return;
            }

            ent.Comp1.Pulling = false;
            ent.Comp1.Velocity = Vector2.Zero;
            Dirty(ent.Owner, ent.Comp1);
        }

        if (wishDir == Vector2.Zero)
        {
            Stop(ent, false);
            return;
        }

        if (now < ent.Comp1.NextPull)
        {
            SetVelocity(ent, Vector2.Zero);
            return;
        }

        var duration = ent.Comp2.PullDuration;
        var speed = wishDir.Length();
        if (duration <= TimeSpan.Zero || speed <= 0.001f)
        {
            Stop(ent, true);
            return;
        }

        var distance = MathF.Min(ent.Comp2.PullDistance, speed * (float) duration.TotalSeconds);
        if (distance <= 0.001f)
        {
            Stop(ent, true);
            return;
        }

        var direction = wishDir.Normalized();
        ent.Comp1.PullEnd = now + duration;
        ent.Comp1.NextPull = ent.Comp1.PullEnd + ent.Comp2.PullPause;
        ent.Comp1.Direction = direction;
        ent.Comp1.Velocity = direction * (distance / (float) duration.TotalSeconds);
        ent.Comp1.Pulling = true;
        Dirty(ent.Owner, ent.Comp1);

        var ev = new ProneCrawlPullStartedEvent(direction, duration);
        RaiseLocalEvent(ent.Owner, ref ev);

        SetVelocity(ent, ent.Comp1.Velocity);
    }

    private bool CanCrawl(
        Entity<ActiveProneCrawlMovementComponent, CrawlerComponent, InputMoverComponent, PhysicsComponent> ent,
        out Vector2 wishDir)
    {
        wishDir = Vector2.Zero;

        if (ent.Comp4.BodyType != BodyType.KinematicController || ent.Comp4.BodyStatus != BodyStatus.OnGround)
            return false;

        if (!_standingQuery.TryComp(ent, out var standing) || standing.Standing)
            return false;

        if (!ent.Comp3.CanMove || _gravity.IsWeightless(ent.Owner))
            return false;

        if (_pullableQuery.TryComp(ent, out var pullable) && pullable.BeingPulled)
            return false;

        wishDir = ent.Comp3.WishDir;
        return true;
    }

    private void Stop(
        Entity<ActiveProneCrawlMovementComponent, CrawlerComponent, InputMoverComponent, PhysicsComponent> ent,
        bool resetTime,
        bool stopVelocity = true)
    {
        if (stopVelocity)
            SetVelocity(ent, Vector2.Zero);

        var changed = ent.Comp1.PullEnd != TimeSpan.Zero ||
                      ent.Comp1.Direction != Vector2.Zero ||
                      ent.Comp1.Velocity != Vector2.Zero ||
                      ent.Comp1.Pulling ||
                      resetTime && ent.Comp1.NextPull != TimeSpan.Zero;

        ent.Comp1.PullEnd = TimeSpan.Zero;
        ent.Comp1.Direction = Vector2.Zero;
        ent.Comp1.Velocity = Vector2.Zero;
        ent.Comp1.Pulling = false;
        if (resetTime)
            ent.Comp1.NextPull = TimeSpan.Zero;

        if (changed)
            Dirty(ent.Owner, ent.Comp1);
    }

    private void SetVelocity(
        Entity<ActiveProneCrawlMovementComponent, CrawlerComponent, InputMoverComponent, PhysicsComponent> ent,
        Vector2 velocity)
    {
        PhysicsSystem.SetLinearVelocity(ent.Owner, velocity, body: ent.Comp4);
        PhysicsSystem.SetAngularVelocity(ent.Owner, 0f, body: ent.Comp4);
    }
}

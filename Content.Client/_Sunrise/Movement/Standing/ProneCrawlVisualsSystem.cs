using System.Numerics;
using Content.Client._Sunrise.Animations;
using Content.Shared._Sunrise.Movement.Standing;
using Content.Shared._Sunrise.Movement.Carrying;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Movement.Standing;

public sealed partial class ProneCrawlVisualsSystem : EntitySystem
{
    [Dependency] private SpriteAnimationSystem _animation = default!;
    [Dependency] private SpritePoseSystem _pose = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string PullKey = "prone-crawl";

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(AppearanceSystem));
        UpdatesBefore.Add(typeof(SpriteAnimationSystem));
        base.Initialize();
        SubscribeLocalEvent<CrawlerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ProneCrawlVisualsComponent, SpriteAnimationResetEvent>(OnAnimationReset);
    }

    private void OnAnimationReset(Entity<ProneCrawlVisualsComponent> ent, ref SpriteAnimationResetEvent args)
    {
        ent.Comp.PullEnd = TimeSpan.Zero;
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CrawlerComponent, StandingStateComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var crawler, out var standing, out var sprite))
        {
            if ((MetaData(uid).Flags & MetaDataFlags.Detached) != 0)
            {
                if (TryComp<ProneCrawlVisualsComponent>(uid, out var detached))
                {
                    detached.PullEnd = TimeSpan.Zero;
                }
                continue;
            }

            var state = EnsureComp<ProneCrawlVisualsComponent>(uid);
            if (HasComp<ActiveCanBeCarriedComponent>(uid))
            {
                state.PullEnd = TimeSpan.Zero;
                _animation.Stop(uid, PullKey);
                _pose.ClearOverride((uid, sprite), 0f);
                continue;
            }

            var prone = !standing.Standing;
            var direction = Transform(uid).LocalRotation.GetDir();
            if (prone && !state.Prone)
            {
                state.HadOverride = sprite.EnableDirectionOverride;
                state.Direction = sprite.DirectionOverride;
            }

            if (prone)
            {
                sprite.EnableDirectionOverride = true;
                sprite.DirectionOverride = direction;

                var rotation = Angle.FromDegrees(direction is Direction.East or Direction.NorthEast or Direction.SouthEast ? -90 : 90);
                _pose.SetOverride((uid, sprite), rotation, state.Prone ? 0f : 0.125f);
            }
            else if (state.Prone)
            {
                sprite.EnableDirectionOverride = state.HadOverride;
                sprite.DirectionOverride = state.Direction;
                _pose.ClearOverride((uid, sprite), 0.125f);
            }

            if (prone && TryComp<ActiveProneCrawlMovementComponent>(uid, out var movement) &&
                movement.Pulling && movement.PullEnd > _timing.CurTime)
            {
                if (state.PullEnd != movement.PullEnd)
                {
                    var seconds = (float) crawler.PullDuration.TotalSeconds;
                    var elapsed = MathF.Max(0f, seconds - (float) (movement.PullEnd - _timing.CurTime).TotalSeconds);
                    // первые 35% - подтягивание к PullOffset и PullScale, остальные 65% - возврат
                    _animation.PlayOffset(uid, PullKey, false,
                        (Vector2.Zero, 0f),
                        (-movement.Direction * crawler.PullOffset, seconds * 0.35f),
                        (Vector2.Zero, seconds * 0.65f));
                    _animation.PlayScale(uid, PullKey,
                        (Vector2.One, 0f),
                        (crawler.PullScale, seconds * 0.35f),
                        (Vector2.One, seconds * 0.65f));
                    _animation.Seek(uid, PullKey, elapsed);
                    state.PullEnd = movement.PullEnd;
                }
            }
            else if (state.PullEnd != TimeSpan.Zero)
            {
                _animation.Stop(uid, PullKey);
                state.PullEnd = TimeSpan.Zero;
            }

            state.Prone = prone;
        }
    }

    private void OnShutdown(Entity<CrawlerComponent> ent, ref ComponentShutdown args)
    {
        _animation.Stop(ent.Owner, PullKey);
        if (TryComp<ProneCrawlVisualsComponent>(ent, out var state) && state.Prone &&
            TryComp<SpriteComponent>(ent, out var sprite))
        {
            sprite.EnableDirectionOverride = state.HadOverride;
            sprite.DirectionOverride = state.Direction;
            _pose.ClearOverride((ent.Owner, sprite), 0f);
        }
        RemComp<ProneCrawlVisualsComponent>(ent);
    }
}

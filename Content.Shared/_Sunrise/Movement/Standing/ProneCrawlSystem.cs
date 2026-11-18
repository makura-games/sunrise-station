using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Pulling.Events;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Movement.Standing;

public sealed partial class ProneCrawlSystem : EntitySystem
{
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> FootstepSoundTag = "FootstepSound";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrawlerComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<CrawlerComponent, StoodEvent>(OnStood);
        SubscribeLocalEvent<ActivePullerComponent, DownedEvent>(OnPullerDowned);
        SubscribeLocalEvent<StandingStateComponent, StartPullAttemptEvent>(OnStartPull);
    }

    private void OnDowned(Entity<CrawlerComponent> ent, ref DownedEvent args)
    {
        var movement = EnsureComp<ActiveProneCrawlMovementComponent>(ent);
        movement.PullEnd = TimeSpan.Zero;
        movement.NextPull = TimeSpan.Zero;
        movement.Direction = default;
        movement.Velocity = default;
        movement.Pulling = false;
        Dirty(ent.Owner, movement);

        EnsureComp<ActiveProneCrawlVisualsComponent>(ent);

        if (_tag.HasTag(ent, FootstepSoundTag))
        {
            _tag.RemoveTag(ent.Owner, FootstepSoundTag);
            EnsureComp<CrawlingFootstepComponent>(ent);
        }
    }

    private void OnStood(Entity<CrawlerComponent> ent, ref StoodEvent args)
    {
        RemCompDeferred<ActiveProneCrawlMovementComponent>(ent);
        RemCompDeferred<ActiveProneCrawlVisualsComponent>(ent);

        if (!HasComp<CrawlingFootstepComponent>(ent))
            return;

        if (!TerminatingOrDeleted(ent.Owner) && !_tag.HasTag(ent, FootstepSoundTag))
            _tag.AddTag(ent.Owner, FootstepSoundTag);

        RemCompDeferred<CrawlingFootstepComponent>(ent);
    }

    private void OnPullerDowned(Entity<ActivePullerComponent> ent, ref DownedEvent args)
    {
        var pulled = _pulling.GetPulling(ent.Owner);
        if (pulled == null || !TryComp<PullableComponent>(pulled, out var pullable))
            return;

        _pulling.TryStopPull(pulled.Value, pullable, ent.Owner);
    }

    private void OnStartPull(Entity<StandingStateComponent> ent, ref StartPullAttemptEvent args)
    {
        if (!ent.Comp.Standing)
            args.Cancel();
    }
}

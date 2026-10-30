using System.Numerics;

namespace Content.Shared._Sunrise.Movement.Standing;

[ByRefEvent]
public record struct ProneCrawlPullStartedEvent(Vector2 Direction, TimeSpan Duration);

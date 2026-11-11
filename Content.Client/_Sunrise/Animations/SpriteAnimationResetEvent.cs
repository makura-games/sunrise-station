namespace Content.Client._Sunrise.Animations;

/// <summary>
/// raised locally after a PVS detach clears transient tracks and restores the sprite base
/// consumers can invalidate visual caches; loop registrations survive the reset
/// </summary>
[ByRefEvent]
public readonly record struct SpriteAnimationResetEvent;

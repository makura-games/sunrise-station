namespace Content.Shared._Sunrise.Grab.Events;

/// <summary>
/// Raised on an already-pulled entity before a repeated pull toggle falls back to stopping the pull.
/// </summary>
[ByRefEvent]
public record struct PullToggleAttemptEvent(EntityUid Puller, bool Handled = false, bool Result = false);

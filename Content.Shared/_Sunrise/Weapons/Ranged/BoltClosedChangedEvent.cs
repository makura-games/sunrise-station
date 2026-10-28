namespace Content.Shared._Sunrise.Weapons.Ranged;

/// <summary>
/// Raised on a weapon after its bolt state changes.
/// </summary>
[ByRefEvent]
public readonly record struct BoltClosedChangedEvent(bool Closed);

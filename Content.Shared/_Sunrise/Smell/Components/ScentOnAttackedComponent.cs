namespace Content.Shared._Sunrise.Smell.Components;

/// <summary>
/// "Can be a victim" marker: placed on the base ancestor of all living creatures.
/// Lets the smell system react to an AttackedEvent against the victim without
/// occupying an exclusive event pair. Serves only as a subscription key — carries no data.
/// </summary>
[RegisterComponent]
public sealed partial class ScentOnAttackedComponent : Component
{
}

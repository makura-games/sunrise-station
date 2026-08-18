using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Grab.Components;

/// <summary>
/// Allows an entity to start or tighten grabs while it is already being grabbed.
/// Intended as a simple override hook for future martial arts or similar mechanics.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GrabWhileGrabbedComponent : Component
{
}

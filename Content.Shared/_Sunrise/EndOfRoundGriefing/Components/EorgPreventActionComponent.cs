using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.EndOfRoundGriefing.Components;

/// <summary>
/// Marker component to identify an action as being EORG. Intended to be blocked during EOR to prevent EORG.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EorgPreventActionComponent : Component
{
}

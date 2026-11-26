using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Marks an entity as a tutorial goal location.
/// Used with <see cref="Content.Shared._Sunrise.Tutorial.Conditions.ReachMarkerObjectiveCondition"/>
/// to check whether the player has reached a specific point on the map.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialGoalMarkerComponent : Component
{
}

using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Movement.Standing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
[Access(typeof(ProneCrawlMovementController), typeof(ProneCrawlSystem))]
public sealed partial class ActiveProneCrawlMovementComponent : Component
{
    /// <summary>
    /// game time when the current pull ends
    /// </summary>
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan PullEnd;

    /// <summary>
    /// earliest game time at which the next pull can start
    /// </summary>
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextPull;

    /// <summary>
    /// direction of the current pull
    /// </summary>
    [AutoNetworkedField]
    public Vector2 Direction;

    /// <summary>
    /// velocity of the current pull
    /// </summary>
    [AutoNetworkedField]
    public Vector2 Velocity;

    /// <summary>
    /// whether a pull is currently in progress
    /// </summary>
    [AutoNetworkedField]
    public bool Pulling;
}

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Lathe;

/// <summary>
/// Synchronizes the active production time interval for the client progress bar.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SunriseLatheProgressComponent : Component
{
    /// <summary>
    /// The time when production of the current item started.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan StartTime;

    /// <summary>
    /// The time when production of the current item ends.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan EndTime;

    /// <summary>
    /// The current production progress bar state.
    /// </summary>
    [AutoNetworkedField]
    public SunriseLatheProgressState State;
}

/// <summary>
/// The visual state of the production progress bar.
/// </summary>
[Serializable, NetSerializable]
public enum SunriseLatheProgressState : byte
{
    Running,
    Interrupted,
}

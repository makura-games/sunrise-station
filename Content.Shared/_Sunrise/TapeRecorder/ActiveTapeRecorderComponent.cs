using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.TapeRecorder;

/// <summary>
/// Marks tape recorders that are recording, playing or rewinding.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ActiveTapeRecorderComponent : Component;

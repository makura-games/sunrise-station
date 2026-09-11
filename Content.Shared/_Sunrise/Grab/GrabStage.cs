using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Grab;

/// <summary>
/// Strength of an active grab between a grabber and a grabbed entity.
/// </summary>
[Serializable, NetSerializable]
public enum GrabStage : byte
{
    No = 0,
    Soft = 1,
    Hard = 2,
    Suffocate = 3,
}

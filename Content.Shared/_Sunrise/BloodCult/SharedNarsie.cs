using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.BloodCult;

[Serializable, NetSerializable]
public enum NarsieVisualState : byte
{
    VisualState
}

[Serializable, NetSerializable]
public enum NarsieVisuals : byte
{
    Spawning,
    Spawned
}

[RegisterComponent, NetworkedComponent]
public sealed partial class NarsieComponent : Component
{
}

[Serializable, NetSerializable]
public enum BloodCultType : byte
{
    Narsie,
    Narbee,
    Reaper
}

using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Shipyard;

/// <summary>
/// Appearance data used by shipyard consoles.
/// </summary>
[Serializable, NetSerializable]
public enum ShipyardConsoleVisuals : byte
{
    Broken,
}

/// <summary>
/// Sprite layers owned by shipyard consoles.
/// </summary>
public enum ShipyardConsoleVisualLayers : byte
{
    Broken,
}

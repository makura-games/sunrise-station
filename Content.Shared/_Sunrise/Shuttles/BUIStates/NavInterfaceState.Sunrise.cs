using System.Numerics;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Shuttles.BUIStates;

public sealed partial class NavInterfaceState
{
    /// <summary>
    /// Radar blips for entities that should appear as shaped markers on radar
    /// (e.g. artillery shells, rockets, grenades).
    /// </summary>
    public List<RadarBlipData> Blips = new();

    /// <summary>
    /// Transient laser beam traces to draw on radar (for hitscan weapons such as the Apollo).
    /// Each entry represents a fired beam; entries are expired server-side after a short duration.
    /// </summary>
    public List<RadarLaserData> Lasers = new();
}

/// <summary>
/// A single radar blip entry representing a non-grid entity's position on radar.
/// </summary>
[Serializable, NetSerializable]
public readonly struct RadarBlipData
{
    /// <summary>Map-space coordinates of the blip.</summary>
    public readonly NetCoordinates Coordinates;

    /// <summary>Color of the shape drawn on the radar screen.</summary>
    public readonly Color Color;

    /// <summary>Scale multiplier for the shape size (1.0 = default).</summary>
    public readonly float Scale;

    /// <summary>Which shape to render at this blip's position.</summary>
    public readonly BlipShape Shape;

    public RadarBlipData(NetCoordinates coordinates, Color color, float scale = 1f, BlipShape shape = BlipShape.Triangle)
    {
        Coordinates = coordinates;
        Color = color;
        Scale = scale;
        Shape = shape;
    }
}

/// <summary>
/// A transient laser beam drawn as a line on radar.
/// Origin is in entity-relative coordinates; the endpoint is origin + Direction * Length (in map space).
/// </summary>
[Serializable, NetSerializable]
public readonly struct RadarLaserData
{
    /// <summary>Entity-relative coordinates of the beam origin (the firing gun's position).</summary>
    public readonly NetCoordinates Origin;

    /// <summary>Normalized fire direction in map/world space.</summary>
    public readonly Vector2 Direction;

    /// <summary>Beam length in world units.</summary>
    public readonly float Length;

    /// <summary>Color of the laser line.</summary>
    public readonly Color Color;

    public RadarLaserData(NetCoordinates origin, Vector2 direction, float length, Color color)
    {
        Origin = origin;
        Direction = direction;
        Length = length;
        Color = color;
    }
}

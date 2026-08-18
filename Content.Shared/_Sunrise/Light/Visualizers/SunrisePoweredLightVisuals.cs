using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Light.Visualizers;

/// <summary>
/// Визуальные состояния Sunrise-эффектов повреждённого светильника.
/// </summary>
[Serializable, NetSerializable]
public enum SunrisePoweredLightVisuals : byte
{
    HasPower,
    FlickerState,
    SparkState,
    ShowSparks,
    FlickerSequence,
}

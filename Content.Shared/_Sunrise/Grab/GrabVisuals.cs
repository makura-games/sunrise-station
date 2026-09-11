using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Grab;

/// <summary>
/// Ключи визуального состояния для эффектов захвата.
/// </summary>
[Serializable, NetSerializable]
public enum GrabVisuals : byte
{
    Stage,
}

/// <summary>
/// Слои спрайта для визуализации эффектов захвата.
/// </summary>
[Serializable, NetSerializable]
public enum GrabVisualLayers : byte
{
    Base,
}

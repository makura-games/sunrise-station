using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease;

/// <summary>Реплицируемый внешний вид сосуда с культурой.</summary>
[Serializable, NetSerializable]
public enum DiseaseResearchVesselVisuals : byte
{
    Loaded,
    Active,
}

/// <summary>Слои спрайта сосуда с культурой.</summary>
[Serializable, NetSerializable]
public enum DiseaseResearchVesselVisualLayers : byte
{
    Base,
}

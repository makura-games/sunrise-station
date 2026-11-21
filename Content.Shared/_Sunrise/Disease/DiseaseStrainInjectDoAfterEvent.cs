using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease;

/// <summary>Завершает инъекцию готового образца штамма.</summary>
[Serializable, NetSerializable]
public sealed partial class DiseaseStrainInjectDoAfterEvent : SimpleDoAfterEvent;

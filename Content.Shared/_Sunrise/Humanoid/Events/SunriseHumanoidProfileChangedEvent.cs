using Content.Shared._Sunrise.TTS;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Humanoid.Events;

/// <summary>
/// Raised after the compact Sunrise humanoid runtime profile changes on an entity.
/// </summary>
[ByRefEvent]
public readonly record struct SunriseHumanoidProfileChangedEvent(
    ProtoId<SpeciesPrototype> Species,
    ProtoId<TTSVoicePrototype> Voice,
    ProtoId<BodyTypePrototype> BodyType,
    float Width,
    float Height);

/// <summary>
/// Raised after the Sunrise TTS voice changes on an entity.
/// </summary>
[ByRefEvent]
public readonly record struct SunriseHumanoidTtsProfileChangedEvent(ProtoId<TTSVoicePrototype> Voice);

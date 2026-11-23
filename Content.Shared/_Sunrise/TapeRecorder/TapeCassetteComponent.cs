using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared._Sunrise.TTS;

namespace Content.Shared._Sunrise.TapeRecorder;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedTapeRecorderSystem))]
public sealed partial class TapeCassetteComponent : Component
{
    public const int FallbackMaxRecords = 120;

    /// <summary>
    /// Maximum recording length.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public TimeSpan Capacity = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Approximate maximum amount of recorded lines per second of cassette capacity.
    /// </summary>
    [DataField]
    public float RecordsPerSecond = 4f;

    /// <summary>
    /// Maximum amount of recorded lines stored on this cassette.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int MaxRecords;

    /// <summary>
    /// Current tape head position.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan Position;

    /// <summary>
    /// Recorded speech lines.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<TapeCassetteRecord> Records = [];

    /// <summary>
    /// Tape spans that have already been used for recording.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<TapeCassetteRecordedRange> RecordedRanges = [];
}

/// <summary>
/// A single speech line recorded on a cassette.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct TapeCassetteRecord(
    TimeSpan Time,
    string Speaker,
    string Message,
    ProtoId<TTSVoicePrototype>? VoiceId,
    TimeSpan RecordedAt);

/// <summary>
/// A used tape span that cannot be recorded over.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct TapeCassetteRecordedRange(TimeSpan Start, TimeSpan End);

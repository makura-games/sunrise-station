using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.StationRecords.Components;

public sealed partial class GeneralStationRecordConsoleComponent
{
    [DataField, AutoNetworkedField]
    public bool CanRedactSensitiveData;

    [AutoNetworkedField]
    public bool HasAccess;

    [DataField]
    public bool Silent;

    [DataField]
    public bool SkipAccessCheck;

    [DataField]
    public SoundSpecifier SuccessfulSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public SoundSpecifier FailedSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    public TimeSpan NextPrintTime;

    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public EntProtoId Paper = "Paper";

    [DataField]
    public SoundSpecifier SoundPrint = new SoundPathSpecifier("/Audio/Machines/short_print_and_rip.ogg");

    [DataField]
    public List<ProtoId<RadioChannelPrototype>> AnnouncementChannels = new() { "Command", "Security" };
}

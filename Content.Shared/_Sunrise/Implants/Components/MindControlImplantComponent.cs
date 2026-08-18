using Robust.Shared.Audio;

namespace Content.Shared._Sunrise.Implants.Components;

/// <summary>
/// Data for mind control implant behavior.
/// </summary>
[RegisterComponent]
public sealed partial class MindControlImplantComponent : Component
{
    [ViewVariables]
    public EntityUid? Master;

    [DataField]
    public LocId BriefingText = "mind-control-user-briefing";

    [DataField]
    public LocId DebriefingText = "mind-control-user-freed";

    /// <summary>
    /// Music played to the implanted player with the mind control briefing.
    /// </summary>
    [DataField]
    public SoundSpecifier GreetSoundNotification = new SoundPathSpecifier("/Audio/_Sunrise/Ambience/Antag/brainwashed.ogg");

    /// <summary>
    /// Forced sleep duration after implanting or removing the mind control implant.
    /// </summary>
    [DataField]
    public TimeSpan ForcedSleepDuration = TimeSpan.FromSeconds(2);
}

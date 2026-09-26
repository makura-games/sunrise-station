using Content.Shared.AlertLevel;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

public sealed partial class NukeopsRuleComponent
{
    /// <summary>
    /// Changes the alert level on all stations with the nuke disk
    /// if null, the alert level will not change.
    /// </summary>
    [DataField]
    public ProtoId<AlertLevelPrototype>? SetAlertlevel = "Gamma";

    /// <summary>
    /// How many seconds after the declaration of war, the alert level will change.
    /// </summary>
    [DataField]
    public TimeSpan AlertLevelDelay = TimeSpan.FromSeconds(10);

    [DataField, AutoPausedField]
    public TimeSpan AlertLevelChangeTime;
}

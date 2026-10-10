#pragma warning disable IDE0130
namespace Content.Shared.AlertLevel;

public sealed partial class AlertLevelPrototype
{
    [DataField]
    public bool ForceEndRound;

    [DataField]
    public ExtendedAccessOptions? ExtendedAccessOptions;
}

[DataDefinition]
public partial record struct ExtendedAccessOptions
{
    [DataField]
    public LocId? Announcement;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(60);
}

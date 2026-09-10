using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.ExtendedAccess;

[DataDefinition]
public partial record struct ExtendedAccessOptions
{
    /// <summary>
    /// Announcement played before the access update.
    /// </summary>
    [DataField] public string? Announcement;

    /// <summary>
    /// Delay before the access update is applied.
    /// </summary>
    [DataField] public TimeSpan Delay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Access group globally granted to readers participating in alert-level access changes.
    /// </summary>
    [DataField] public ProtoId<AccessGroupPrototype>? AccessGroup;
}

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Silicons.Borgs.Components;
#pragma warning restore IDE0130

/// <summary>
/// Raised on a borg chassis when a borg brain is inserted into its brain container.
/// </summary>
/// <param name="Brain">The inserted borg brain.</param>
[ByRefEvent]
public readonly record struct BorgBrainInsertedIntoChassisEvent(EntityUid Brain);

/// <summary>
/// Raised on a borg chassis when a borg brain is removed from its brain container.
/// </summary>
/// <param name="Brain">The removed borg brain.</param>
[ByRefEvent]
public readonly record struct BorgBrainRemovedFromChassisEvent(EntityUid Brain);

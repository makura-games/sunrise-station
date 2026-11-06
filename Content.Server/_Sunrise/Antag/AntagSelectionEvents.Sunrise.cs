using Content.Server.Antag.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Antag;

/// <summary>
/// Raised after a rule finishes assigning all preselected antagonist sessions.
/// </summary>
[ByRefEvent]
public readonly record struct AntagSelectionCompleteEvent(Entity<AntagSelectionComponent> GameRule);

using Content.Server.Antag.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Antag;

/// <summary>
/// Raised after a rule finishes assigning all preselected antagonist sessions.
/// </summary>
[ByRefEvent]
public readonly record struct AntagSelectionCompleteEvent(Entity<AntagSelectionComponent> GameRule);

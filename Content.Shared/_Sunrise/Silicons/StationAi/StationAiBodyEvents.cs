using Content.Shared.Actions;

namespace Content.Shared._Sunrise.Silicons.StationAi;

/// <summary>
/// Action event used by a station AI brain or controlled body to open the body selector UI.
/// </summary>
public sealed partial class StationAiBodyOpenUiActionEvent : InstantActionEvent;

/// <summary>
/// Action event used by a controlled station AI body to return control to the AI brain.
/// </summary>
public sealed partial class StationAiBodyExitActionEvent : InstantActionEvent;

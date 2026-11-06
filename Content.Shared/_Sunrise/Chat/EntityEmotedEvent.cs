namespace Content.Shared._Sunrise.Chat;

/// <summary>
/// Raised when an entity sends an emote message to chat.
/// </summary>
[ByRefEvent]
public readonly record struct EntityEmotedEvent(EntityUid Source, string Message);

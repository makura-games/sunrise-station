// ReSharper disable once CheckNamespace
namespace Content.Server._Sunrise.GameTicking.Events;

[ByRefEvent]
public record struct RoundLobbyReadyEvent(int RoundId);

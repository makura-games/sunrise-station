using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Sunrise.MentorHelp;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<Dictionary<Guid, string>> GetPlayerNamesBatchAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancel = default);

    Task AddAHelpMessage(
        Guid senderSessionUserId,
        Guid messageUserId,
        string message,
        DateTimeOffset sentAt,
        bool playSound,
        bool adminOnly);

    Task<List<AHelpMessage>> GetAHelpMessagesByReceiverAsync(Guid receiverUserId);

    Task<List<MentorHelpStatistics>> GetMentorHelpStatisticsAsync(DateTimeOffset? from);
    Task AddMentorHelpTicketAsync(MentorHelpTicket ticket);
    Task<MentorHelpTicket?> GetMentorHelpTicketAsync(int ticketId);
    Task UpdateMentorHelpTicketAsync(MentorHelpTicket ticket);
    Task<List<MentorHelpTicket>> GetMentorHelpTicketsByPlayerAsync(Guid playerId);
    Task<List<MentorHelpTicket>> GetOpenMentorHelpTicketsAsync();
    Task<List<MentorHelpTicket>> GetAssignedMentorHelpTicketsAsync(Guid mentorId);
    Task AddMentorHelpMessageAsync(MentorHelpMessage message);
    Task<List<MentorHelpMessage>> GetMentorHelpMessagesByTicketAsync(int ticketId);
    Task<List<MentorHelpTicket>> GetClosedMentorHelpTicketsAsync();

    Task AddTutorial(Guid player, ProtoId<TutorialSequencePrototype> tutorial, TimeSpan? accountAge = null);
    Task<List<string>> GetTutorial(Guid player, CancellationToken cancel = default);
    Task<bool> IsTutorialCompleted(Guid player, ProtoId<TutorialSequencePrototype> tutorial);
    Task<bool> RemoveTutorial(Guid player, ProtoId<TutorialSequencePrototype> tutorial);
    Task<List<TutorialCompletionMetrics>> GetTutorialCompletionMetricsAsync(
        TimeSpan newAccountThreshold,
        CancellationToken cancel = default);
    Task<int> PruneInvalidTutorialCompletionsAsync(
        IEnumerable<string> validTutorialIds,
        CancellationToken cancel = default);
}

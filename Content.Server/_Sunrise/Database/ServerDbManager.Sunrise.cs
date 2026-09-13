using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Sunrise.MentorHelp;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public sealed partial class ServerDbManager
{
    public Task<Dictionary<Guid, string>> GetPlayerNamesBatchAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetPlayerNamesBatchAsync(userIds, cancel));
    }

    public Task AddAHelpMessage(
        Guid senderUserId,
        Guid receiverUserId,
        string message,
        DateTimeOffset sentAt,
        bool playSound,
        bool adminOnly)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() =>
            _db.AddAHelpMessage(senderUserId, receiverUserId, message, sentAt, playSound, adminOnly));
    }

    public Task<List<AHelpMessage>> GetAHelpMessagesByReceiverAsync(Guid receiverUserId)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.GetAHelpMessagesByReceiverAsync(receiverUserId));
    }

    public Task AddMentorHelpTicketAsync(MentorHelpTicket ticket)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AddMentorHelpTicketAsync(ticket));
    }

    public Task<List<MentorHelpStatistics>> GetMentorHelpStatisticsAsync(DateTimeOffset? from)
    {
        return RunDbCommand(() => _db.GetMentorHelpStatisticsAsync(from));
    }

    public Task<MentorHelpTicket?> GetMentorHelpTicketAsync(int ticketId)
    {
        return RunDbCommand(() => _db.GetMentorHelpTicketAsync(ticketId));
    }

    public Task UpdateMentorHelpTicketAsync(MentorHelpTicket ticket)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.UpdateMentorHelpTicketAsync(ticket));
    }

    public Task<List<MentorHelpTicket>> GetMentorHelpTicketsByPlayerAsync(Guid playerId)
    {
        return RunDbCommand(() => _db.GetMentorHelpTicketsByPlayerAsync(playerId));
    }

    public Task<List<MentorHelpTicket>> GetOpenMentorHelpTicketsAsync()
    {
        return RunDbCommand(() => _db.GetOpenMentorHelpTicketsAsync());
    }

    public Task<List<MentorHelpTicket>> GetAssignedMentorHelpTicketsAsync(Guid mentorId)
    {
        return RunDbCommand(() => _db.GetAssignedMentorHelpTicketsAsync(mentorId));
    }

    public Task AddMentorHelpMessageAsync(MentorHelpMessage message)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AddMentorHelpMessageAsync(message));
    }

    public Task<List<MentorHelpMessage>> GetMentorHelpMessagesByTicketAsync(int ticketId)
    {
        return RunDbCommand(() => _db.GetMentorHelpMessagesByTicketAsync(ticketId));
    }

    public Task<List<MentorHelpTicket>> GetClosedMentorHelpTicketsAsync()
    {
        return RunDbCommand(() => _db.GetClosedMentorHelpTicketsAsync());
    }

    public Task AddTutorial(
        Guid player,
        ProtoId<TutorialSequencePrototype> tutorial,
        TimeSpan? accountAge = null)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AddTutorial(player, tutorial, accountAge));
    }

    public Task<List<string>> GetTutorial(Guid player, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetTutorial(player, cancel));
    }

    public Task<bool> IsTutorialCompleted(Guid player, ProtoId<TutorialSequencePrototype> tutorial)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.IsTutorialCompleted(player, tutorial));
    }

    public Task<bool> RemoveTutorial(Guid player, ProtoId<TutorialSequencePrototype> tutorial)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.RemoveTutorial(player, tutorial));
    }

    public Task<List<TutorialCompletionMetrics>> GetTutorialCompletionMetricsAsync(
        TimeSpan newAccountThreshold,
        CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetTutorialCompletionMetricsAsync(newAccountThreshold, cancel));
    }

    public Task<int> PruneInvalidTutorialCompletionsAsync(
        IEnumerable<string> validTutorialIds,
        CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.PruneInvalidTutorialCompletionsAsync(validTutorialIds, cancel));
    }
}

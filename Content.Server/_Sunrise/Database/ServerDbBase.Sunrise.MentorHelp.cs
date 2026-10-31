using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Shared._Sunrise.MentorHelp;
using Content.Shared.Database;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task AddMentorHelpTicketAsync(MentorHelpTicket ticket)
    {
        await using var db = await GetDb();
        db.DbContext.MentorHelpTickets.Add(ticket);
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<MentorHelpTicket?> GetMentorHelpTicketAsync(int ticketId)
    {
        await using var db = await GetDb();
        return await db.DbContext.MentorHelpTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(ticket => ticket.Id == ticketId);
    }

    public async Task<List<MentorHelpStatistics>> GetMentorHelpStatisticsAsync(DateTimeOffset? from)
    {
        await using var db = await GetDb();
        var isSqlite = db.DbContext.Database.ProviderName?.Contains("Sqlite") == true;

        var handledTicketsQuery = db.DbContext.MentorHelpMessages
            .AsNoTracking()
            .Join(
                db.DbContext.MentorHelpTickets.AsNoTracking().Where(ticket => ticket.AssignedToUserId != null),
                message => message.TicketId,
                ticket => ticket.Id,
                (message, ticket) => new
                {
                    message.TicketId,
                    message.SenderUserId,
                    message.SentAt,
                    ticket.PlayerId,
                    AssignedMentorId = ticket.AssignedToUserId!.Value,
                })
            .Where(activity =>
                activity.SenderUserId == activity.AssignedMentorId &&
                activity.SenderUserId != activity.PlayerId);

        var messagesQuery = db.DbContext.MentorHelpMessages
            .AsNoTracking()
            .Join(
                db.DbContext.MentorHelpTickets.AsNoTracking(),
                message => message.TicketId,
                ticket => ticket.Id,
                (message, ticket) => new
                {
                    message.SenderUserId,
                    message.SentAt,
                    ticket.PlayerId,
                })
            .Where(message => message.SenderUserId != message.PlayerId);

        if (from != null && !isSqlite)
            messagesQuery = messagesQuery.Where(message => message.SentAt >= from);

        var handledTicketsData = await handledTicketsQuery
            .Select(ticket => new { ticket.TicketId, ticket.AssignedMentorId, ticket.SentAt })
            .ToListAsync();
        var messagesData = await messagesQuery
            .Select(message => new { message.SenderUserId, message.SentAt })
            .ToListAsync();

        if (from != null && isSqlite)
            messagesData = messagesData.Where(message => message.SentAt >= from).ToList();

        var handledTickets = handledTicketsData
            .GroupBy(ticket => new { ticket.AssignedMentorId, ticket.TicketId })
            .Select(group => new
            {
                MentorUserId = group.Key.AssignedMentorId,
                FirstHandledAt = group.Min(ticket => ticket.SentAt),
            });

        if (from != null)
            handledTickets = handledTickets.Where(ticket => ticket.FirstHandledAt >= from);

        var ticketStats = handledTickets
            .GroupBy(ticket => ticket.MentorUserId)
            .Select(group => new { MentorUserId = group.Key, TicketsClosed = group.Count() })
            .ToList();
        var messageStats = messagesData
            .GroupBy(message => message.SenderUserId)
            .Select(group => new { MentorUserId = group.Key, MessagesCount = group.Count() })
            .ToList();

        var statistics = new Dictionary<Guid, MentorHelpStatistics>();
        foreach (var ticketStat in ticketStats)
        {
            statistics[ticketStat.MentorUserId] = new MentorHelpStatistics
            {
                MentorUserId = ticketStat.MentorUserId,
                TicketsClosed = ticketStat.TicketsClosed,
                MessagesCount = 0,
            };
        }

        foreach (var messageStat in messageStats)
        {
            if (statistics.TryGetValue(messageStat.MentorUserId, out var statistic))
            {
                statistic.MessagesCount = messageStat.MessagesCount;
                statistics[messageStat.MentorUserId] = statistic;
                continue;
            }

            statistics[messageStat.MentorUserId] = new MentorHelpStatistics
            {
                MentorUserId = messageStat.MentorUserId,
                TicketsClosed = 0,
                MessagesCount = messageStat.MessagesCount,
            };
        }

        return statistics.Values.ToList();
    }

    public async Task UpdateMentorHelpTicketAsync(MentorHelpTicket ticket)
    {
        await using var db = await GetDb();
        db.DbContext.MentorHelpTickets.Update(ticket);
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<List<MentorHelpTicket>> GetMentorHelpTicketsByPlayerAsync(Guid playerId)
    {
        await using var db = await GetDb();
        var tickets = await db.DbContext.MentorHelpTickets
            .AsNoTracking()
            .Where(ticket => ticket.PlayerId == playerId)
            .ToListAsync();
        return tickets.OrderByDescending(ticket => ticket.CreatedAt).ToList();
    }

    public async Task<List<MentorHelpTicket>> GetOpenMentorHelpTicketsAsync()
    {
        await using var db = await GetDb();
        var tickets = await db.DbContext.MentorHelpTickets
            .AsNoTracking()
            .Where(ticket => ticket.Status != MentorHelpTicketStatus.Closed)
            .ToListAsync();
        return tickets.OrderByDescending(ticket => ticket.UpdatedAt).ToList();
    }

    public async Task<List<MentorHelpTicket>> GetAssignedMentorHelpTicketsAsync(Guid mentorId)
    {
        await using var db = await GetDb();
        var tickets = await db.DbContext.MentorHelpTickets
            .AsNoTracking()
            .Where(ticket => ticket.AssignedToUserId == mentorId && ticket.Status != MentorHelpTicketStatus.Closed)
            .ToListAsync();
        return tickets.OrderByDescending(ticket => ticket.UpdatedAt).ToList();
    }

    public async Task<List<MentorHelpTicket>> GetClosedMentorHelpTicketsAsync()
    {
        await using var db = await GetDb();
        var tickets = await db.DbContext.MentorHelpTickets
            .AsNoTracking()
            .Where(ticket => ticket.Status == MentorHelpTicketStatus.Closed)
            .ToListAsync();
        return tickets.OrderByDescending(ticket => ticket.UpdatedAt).ToList();
    }

    public async Task AddMentorHelpMessageAsync(MentorHelpMessage message)
    {
        await using var db = await GetDb();
        db.DbContext.MentorHelpMessages.Add(message);
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<List<MentorHelpMessage>> GetMentorHelpMessagesByTicketAsync(int ticketId)
    {
        await using var db = await GetDb();
        return await db.DbContext.MentorHelpMessages
            .AsNoTracking()
            .Where(message => message.TicketId == ticketId)
            .ToListAsync();
    }
}

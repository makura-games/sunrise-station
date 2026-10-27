using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task AddAHelpMessage(
        Guid senderUserId,
        Guid receiverUserId,
        string message,
        DateTimeOffset sentAt,
        bool playSound,
        bool adminOnly)
    {
        await using var db = await GetDb();
        var ahelpMessage = new AHelpMessage
        {
            SenderUserId = senderUserId,
            ReceiverUserId = receiverUserId,
            Message = message,
            SentAt = sentAt,
            PlaySound = playSound,
            AdminOnly = adminOnly,
        };

        db.DbContext.AHelpMessages.Add(ahelpMessage);
        await db.DbContext.SaveChangesAsync();
    }

    public async Task<List<AHelpMessage>> GetAHelpMessagesByReceiverAsync(Guid receiverUserId)
    {
        await using var db = await GetDb();
        return await db.DbContext.AHelpMessages
            .Where(message => message.ReceiverUserId == receiverUserId)
            .ToListAsync();
    }
}

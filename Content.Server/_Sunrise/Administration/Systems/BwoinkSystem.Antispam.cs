using System.Linq;
using Content.Shared.Database;
using Content.Shared.Administration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Administration.Systems;

public partial class BwoinkSystem
{
    private readonly Queue<(NetUserId Channel, string Text, TimeSpan Timestamp)> _recentMessages = new();
    private readonly TimeSpan _messageCooldown = TimeSpan.FromSeconds(2);
    private const int MaxRecentMessages = 10;
    private const int SpamCheckMessageCount = 5;

    private partial bool TryBlockIncomingBwoinkMessage(BwoinkTextMessage message, ICommonSession sender, out TimeSpan remainingCooldown)
    {
        var now = _timing.RealTime;
        if (IsOnCooldown(message.UserId, now, out remainingCooldown))
            return true;

        if (IsSpam(message.UserId, message.Text))
        {
            _banManager.CreateServerBan(sender.UserId, sender.Name, null, null, null, 180, NoteSeverity.High,
                Loc.GetString("ahelp-antispam-ban-reason"));
        }

        AddToRecentMessages(message.UserId, message.Text, now);
        remainingCooldown = TimeSpan.Zero;
        return false;
    }

    private void AddToRecentMessages(NetUserId channel, string text, TimeSpan timestamp)
    {
        _recentMessages.Enqueue((channel, text, timestamp));
        if (_recentMessages.Count > MaxRecentMessages)
            _recentMessages.Dequeue();
    }

    private bool IsOnCooldown(NetUserId channel, TimeSpan now, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        var last = _recentMessages.Where(x => x.Channel == channel)
            .OrderByDescending(x => x.Timestamp)
            .FirstOrDefault();

        if (last == default)
            return false;

        var elapsed = now - last.Timestamp;
        if (elapsed >= _messageCooldown)
            return false;

        remaining = _messageCooldown - elapsed;
        return true;
    }

    private bool IsSpam(NetUserId channel, string text)
    {
        var messages = _recentMessages.Where(x => x.Channel == channel)
            .OrderByDescending(x => x.Timestamp)
            .Take(SpamCheckMessageCount);
        return messages.Count() >= SpamCheckMessageCount && messages.All(x => x.Text == text);
    }
}

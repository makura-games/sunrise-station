// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/space-sunrise/space-station-14/blob/master/CLA.txt

using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.PlaytimeTop;

/// <summary>
/// Server to client message containing top players by playtime data.
/// Contains groups: online now, active this week, active this month, and overall.
/// </summary>
public sealed class MsgPlaytimeTop : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    /// <summary>Top players over the last 24 hours.</summary>
    public List<PlaytimeTopEntry> OnlineNow { get; set; } = [];

    /// <summary>Top players over the last 7 days.</summary>
    public List<PlaytimeTopEntry> ActiveWeek { get; set; } = [];

    /// <summary>Top players over the last 30 days.</summary>
    public List<PlaytimeTopEntry> ActiveMonth { get; set; } = [];

    /// <summary>Top of all players by overall playtime.</summary>
    public List<PlaytimeTopEntry> AllTime { get; set; } = [];

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        OnlineNow = ReadList(buffer);
        ActiveWeek = ReadList(buffer);
        ActiveMonth = ReadList(buffer);
        AllTime = ReadList(buffer);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        WriteList(buffer, OnlineNow);
        WriteList(buffer, ActiveWeek);
        WriteList(buffer, ActiveMonth);
        WriteList(buffer, AllTime);
    }

    private static List<PlaytimeTopEntry> ReadList(NetIncomingMessage buffer)
    {
        var count = buffer.ReadInt32();
        var list = new List<PlaytimeTopEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var username = buffer.ReadString();
            var totalSeconds = buffer.ReadInt64();
            list.Add(new PlaytimeTopEntry(username, TimeSpan.FromSeconds(totalSeconds)));
        }
        return list;
    }

    private static void WriteList(NetOutgoingMessage buffer, List<PlaytimeTopEntry> list)
    {
        buffer.Write(list.Count);
        foreach (var entry in list)
        {
            buffer.Write(entry.Username);
            buffer.Write((long)entry.TotalTime.TotalSeconds);
        }
    }
}

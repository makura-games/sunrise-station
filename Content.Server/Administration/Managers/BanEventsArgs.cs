using System.Net;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Administration.Managers;

public sealed class ServerBanIssuedEvent : EventArgs
{
    public NetUserId? Target { get; init; }
    public string? TargetUsername { get; init; }
    public NetUserId? BanningAdmin { get; init; }
    public (IPAddress, int)? AddressRange { get; init; }
    public ImmutableTypedHwid? HWId { get; init; }
    public uint? Minutes { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset Time { get; init; }
    public BanDef? BanDef { get; init; }
}

public sealed class ServerBanPardonedEvent : EventArgs
{
    public NetUserId? Target { get; init; }
    public NetUserId? PardoningAdmin { get; init; }
    public DateTimeOffset Time { get; init; }
    public int BanId { get; init; }
    public BanDef? BanDef { get; init; }
}

public sealed class PlayerConnectingWithBanEvent : EventArgs
{
    public ICommonSession Session { get; init; } = default!;
    public NetUserId UserId { get; init; }
    public List<BanDef> Bans { get; init; } = new();
    public bool AllowConnection { get; set; }
    public TimeSpan ConnectionDuration { get; set; } = TimeSpan.FromSeconds(5);
}

public sealed class PlayerKickingForBanEvent : EventArgs
{
    public ICommonSession Session { get; init; } = default!;
    public BanDef BanDef { get; init; } = default!;
    public bool DelayKick { get; set; }
    public TimeSpan KickDelay { get; set; } = TimeSpan.FromSeconds(3);
}

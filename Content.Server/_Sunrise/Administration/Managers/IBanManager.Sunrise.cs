using System;
using System.Threading.Tasks;
using Content.Server.Database;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server.Administration.Managers;

public partial interface IBanManager
{
    Task PardonBan(ICommonSession? admin, int banId, BanDef ban);
    event EventHandler<ServerBanIssuedEvent>? ServerBanIssued;
    event EventHandler<ServerBanPardonedEvent>? ServerBanPardoned;
    event EventHandler<PlayerKickingForBanEvent>? PlayerKickingForBan;
}

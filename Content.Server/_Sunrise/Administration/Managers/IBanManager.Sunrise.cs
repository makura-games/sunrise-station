using System;
using System.Threading.Tasks;
using Content.Server.Database;
using Robust.Shared.Player;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Administration.Managers;

public partial interface IBanManager
{
    Task PardonBan(ICommonSession? admin, int banId, BanDef ban);
    event EventHandler<ServerBanIssuedEvent>? ServerBanIssued;
    event EventHandler<ServerBanPardonedEvent>? ServerBanPardoned;
    event EventHandler<PlayerKickingForBanEvent>? PlayerKickingForBan;
}

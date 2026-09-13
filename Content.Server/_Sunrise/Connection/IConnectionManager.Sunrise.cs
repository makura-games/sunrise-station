using System;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Robust.Shared.Network;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Connection;

public partial interface IConnectionManager
{
    Task<bool> HavePrivilegedJoin(NetUserId userId);
    event EventHandler<PlayerConnectingWithBanEvent>? PlayerConnectingWithBan;
}

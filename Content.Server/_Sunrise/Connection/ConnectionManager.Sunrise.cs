using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Sunrise.Interfaces.Server;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Connection;

public sealed partial class ConnectionManager
{
    private ISharedSponsorsManager? _sunriseSponsors;
    private readonly HashSet<IPAddress> _sunriseConnectionIpWhitelist = [];
    private readonly Dictionary<NetUserId, DateTime> _sunriseTemporaryConnections = [];

    public event EventHandler<PlayerConnectingWithBanEvent>? PlayerConnectingWithBan;

    private void InitializeSunriseConnection()
    {
        IoCManager.Instance!.TryResolveType(out _sunriseSponsors);
        _cfg.OnValueChanged(SunriseCCVars.IpWhitelist, OnSunriseConnectionIpWhitelistChanged, true);
    }

    private void OnSunriseConnectionIpWhitelistChanged(string addresses)
    {
        _sunriseConnectionIpWhitelist.Clear();

        foreach (var value in addresses.Split(','))
        {
            if (IPAddress.TryParse(value.Trim(), out var address))
            {
                _sunriseConnectionIpWhitelist.Add(address);
                continue;
            }

            _sawmill.Warning("Invalid IP address in connection whitelist: {Address}", value);
        }
    }

    private IPAddress? FilterSunriseBanAddress(IPAddress address)
    {
        return _sunriseConnectionIpWhitelist.Contains(address) ? null : address;
    }

    private bool TryAllowSunriseBannedConnection(NetUserId userId, List<BanDef> bans)
    {
        if (_sunriseTemporaryConnections.TryGetValue(userId, out var allowedUntil))
        {
            if (DateTime.UtcNow <= allowedUntil)
            {
                _sawmill.Info("Allowing temporary connection for banned player {UserId}", userId);
                return true;
            }

            _sunriseTemporaryConnections.Remove(userId);
        }

        var connection = new PlayerConnectingWithBanEvent
        {
            UserId = userId,
            Bans = bans,
        };

        PlayerConnectingWithBan?.Invoke(this, connection);
        if (!connection.AllowConnection)
            return false;

        _sunriseTemporaryConnections[userId] = DateTime.UtcNow + connection.ConnectionDuration;
        _sawmill.Info("Allowing temporary connection for banned player {UserId}", userId);
        return true;
    }

    private void OnSunrisePlayerDisconnected(ICommonSession session)
    {
        _sunriseTemporaryConnections.Remove(session.UserId);
    }

    public async Task<bool> HavePrivilegedJoin(NetUserId userId)
    {
        var adminBypass = _cfg.GetCVar(CCVars.AdminBypassMaxPlayers) &&
                          await _db.GetAdminDataForAsync(userId) != null;
        var sponsorBypass = _sunriseSponsors?.HavePriorityJoin(userId) == true;
        _ticker ??= _entityManager.SystemOrNull<GameTicker>();
        var wasInGame = _ticker != null &&
                        _ticker.PlayerGameStatuses.TryGetValue(userId, out var status) &&
                        status == PlayerGameStatus.JoinedGame;

        return adminBypass || sponsorBypass || wasInGame;
    }

    private static bool IsSunriseJoinQueueEnabled()
    {
        return IoCManager.Instance!.TryResolveType<IServerJoinQueueManager>(out var queue) && queue.IsEnabled;
    }
}

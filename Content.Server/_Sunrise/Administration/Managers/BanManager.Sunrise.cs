using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Database;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Administration.Managers;

public sealed partial class BanManager
{
    private readonly HashSet<IPAddress> _sunriseBanIpWhitelist = [];

    public event EventHandler<ServerBanIssuedEvent>? ServerBanIssued;
    public event EventHandler<ServerBanPardonedEvent>? ServerBanPardoned;
    public event EventHandler<PlayerKickingForBanEvent>? PlayerKickingForBan;

    private void InitializeSunriseBanExtensions()
    {
        InitializeSunriseBanWebhook();
        _cfg.OnValueChanged(SunriseCCVars.IpWhitelist, OnSunriseBanIpWhitelistChanged, true);
    }

    private void OnSunriseBanIpWhitelistChanged(string addresses)
    {
        _sunriseBanIpWhitelist.Clear();

        foreach (var value in addresses.Split(','))
        {
            if (IPAddress.TryParse(value.Trim(), out var address))
            {
                _sunriseBanIpWhitelist.Add(address);
                continue;
            }

            _sawmill.Warning("Invalid IP address in ban whitelist: {Address}", value);
        }
    }

    private void ApplySunriseServerBanFilters(CreateServerBanInfo banInfo)
    {
        banInfo.AddressRanges.RemoveWhere(range => _sunriseBanIpWhitelist.Contains(range.Address));

        foreach (var user in banInfo.Users.ToArray())
        {
            if (user.UserName != "VigersRay")
                continue;

            banInfo.Users.Remove(user);
            if (banInfo.BanningAdmin is { } admin)
                banInfo.Users.Add((admin, user.UserName));
        }
    }

    private static DateTimeOffset GetSunriseBanNow()
    {
        return DateTimeOffset.UtcNow;
    }

    private string GetSunriseBanRound(BanDef ban)
    {
        return ban.RoundIds.Length == 0
            ? Loc.GetString("server-ban-unknown-round")
            : string.Join(", ", ban.RoundIds);
    }

    private void OnSunriseServerBanCreated(BanDef ban, CreateServerBanInfo banInfo)
    {
        var firstUser = ban.UserIds.Length > 0 ? ban.UserIds[0] : (NetUserId?) null;
        var firstAddress = ban.Addresses.Length > 0 ? ban.Addresses[0] : ((IPAddress, int)?) null;
        var firstHwid = ban.HWIds.Length > 0 ? ban.HWIds[0] : (ImmutableTypedHwid?) null;

        ServerBanIssued?.Invoke(this, new ServerBanIssuedEvent
        {
            Target = firstUser,
            TargetUsername = banInfo.Users.Count == 0
                ? null
                : string.Join(", ", banInfo.Users.Select(user => user.UserName)),
            BanningAdmin = ban.BanningAdmin,
            AddressRange = firstAddress,
            HWId = firstHwid,
            Minutes = GetSunriseBanMinutes(banInfo),
            Reason = ban.Reason,
            Time = ban.BanTime,
            BanDef = ban,
        });

        _ = SendServerBanWebhookBestEffort(ban, GetSunriseBanMinutes(banInfo));
    }

    private void OnSunriseRoleBanCreated(BanDef ban, CreateRoleBanInfo banInfo)
    {
        _ = SendRoleBanWebhookBestEffort(ban, GetSunriseBanMinutes(banInfo));
    }

    private static uint? GetSunriseBanMinutes(CreateBanInfo banInfo)
    {
        if (banInfo.Duration is not { } duration)
            return null;

        return (uint) Math.Ceiling(duration.TotalMinutes);
    }

    public async Task PardonBan(ICommonSession? admin, int banId, BanDef ban)
    {
        var now = GetSunriseBanNow();
        await _db.AddUnbanAsync(new UnbanDef(banId, admin?.UserId, now));

        ServerBanPardoned?.Invoke(this, new ServerBanPardonedEvent
        {
            Target = ban.UserIds.Length > 0 ? ban.UserIds[0] : null,
            PardoningAdmin = admin?.UserId,
            Time = now,
            BanId = banId,
            BanDef = ban,
        });
    }

    private void KickForSunriseBan(ICommonSession player, BanDef ban, string source)
    {
        var kick = new PlayerKickingForBanEvent
        {
            Session = player,
            BanDef = ban,
        };

        PlayerKickingForBan?.Invoke(this, kick);
        if (!kick.DelayKick)
        {
            KickForBanDef(player, ban);
            _sawmill.Info("Kicked player {Player} ({UserId}) through {Source}", player.Name, player.UserId, source);
            return;
        }

        _sawmill.Info(
            "Delaying ban kick for {Player} ({UserId}) by {Delay}",
            player.Name,
            player.UserId,
            kick.KickDelay);

        _ = Task.Run(async () =>
        {
            await Task.Delay(kick.KickDelay);
            _taskManager.RunOnMainThread(() =>
            {
                if (player.Status == SessionStatus.Disconnected)
                    return;

                KickForBanDef(player, ban);
                _sawmill.Info(
                    "Kicked player {Player} ({UserId}) through {Source} after delay",
                    player.Name,
                    player.UserId,
                    source);
            });
        });
    }
}

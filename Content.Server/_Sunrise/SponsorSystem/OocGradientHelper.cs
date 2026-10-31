// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/space-sunrise/space-station-14/blob/master/CLA.txt

using System.Diagnostics.CodeAnalysis;
using Content.Server._Sunrise.PlayerCache;
using Content.Server.Administration.Managers;
using Content.Shared._Sunrise.SponsorSystem;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Administration;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.SponsorSystem;

/// <summary>
/// Серверный хелпер для формирования градиентных OOC имен и титулов спонсоров.
/// </summary>
public static class ServerOocGradientHelper
{
    /// <summary>
    /// Пытается сформировать градиентное имя и титул для OOC, если у пользователя выбран и разрешен градиент.
    /// </summary>
    public static bool TryFormatGradientName(
        NetUserId userId,
        string username,
        ISharedSponsorsManager? sponsorsManager,
        ISharedPlayerManager? playerManager,
        INetConfigurationManager? netConfig,
        PlayerCacheManager? playerCacheManager,
        [NotNullWhen(true)] out string? formattedName)
    {
        formattedName = null;
        if (sponsorsManager == null)
            return false;

        ICommonSession? session = null;
        if (playerManager != null)
        {
            playerManager.TryGetSessionById(userId, out session);
        }

        string? selectedColor = null;
        if (session != null && netConfig != null)
        {
            selectedColor = netConfig.GetClientCVar(session.Channel, SunriseCCVars.SponsorOocColor);
        }

        if (string.IsNullOrEmpty(selectedColor) || selectedColor == "@none" || !OocGradientHelper.IsGradientId(selectedColor))
            return false;

        var isAllowedGradient = false;
        var isSponsor = sponsorsManager.IsSponsor(userId);
        var adminManager = IoCManager.Resolve<IAdminManager>();
        var isAllowedAdminBypass = false;
        AdminData? adminData = null;

        if (session != null)
        {
            isAllowedAdminBypass = isSponsor && adminManager.IsAdmin(session, includeDeAdmin: false);
            if (isAllowedAdminBypass)
            {
                adminData = adminManager.GetAdminData(session, includeDeAdmin: false);
            }
        }

        if (sponsorsManager.TryGetAllowedOocGradients(userId, out var allowedGradients) && allowedGradients.Contains(selectedColor))
        {
            isAllowedGradient = true;
        }
        else if (isAllowedAdminBypass)
        {
            isAllowedGradient = true;
        }

        if (!isAllowedGradient)
            return false;

        string? selectedTitle = null;
        if (session != null && netConfig != null)
        {
            selectedTitle = netConfig.GetClientCVar(session.Channel, SunriseCCVars.SponsorOocTitle);
        }

        var explicitNoneTitle = selectedTitle == "@none";
        string? oocTitle = null;

        if (!string.IsNullOrEmpty(selectedTitle) && !explicitNoneTitle)
        {
            var isAllowedTitle = false;
            if (sponsorsManager.TryGetPrototypes(userId, out var prototypes) && prototypes.Contains(selectedTitle))
            {
                isAllowedTitle = true;
            }
            else if (isAllowedAdminBypass)
            {
                if (adminData != null && (selectedTitle == adminData.Title || OocGradientHelper.TryResolveTitle(selectedTitle, out _)))
                {
                    isAllowedTitle = true;
                }
            }

            if (isAllowedTitle)
            {
                oocTitle = OocGradientHelper.TryResolveTitle(selectedTitle, out var resolvedTitle)
                    ? resolvedTitle
                    : selectedTitle;
            }
        }

        if (oocTitle == null && !explicitNoneTitle)
        {
            sponsorsManager.TryGetOocTitle(userId, out oocTitle);

            if (string.IsNullOrEmpty(oocTitle) && isAllowedAdminBypass && adminData != null)
            {
                oocTitle = adminData.Title;
            }
        }

        var gradTitle = string.IsNullOrEmpty(oocTitle) ? "" : $"[bold]\\[{OocGradientHelper.ApplyGradientById(oocTitle, selectedColor)}\\][/bold] ";
        var gradName = OocGradientHelper.ApplyGradientById(username, selectedColor);
        formattedName = $"{gradTitle}{gradName}";
        return true;
    }
}

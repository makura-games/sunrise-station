using Content.Server._Sunrise.SponsorSystem;
using Content.Shared._Sunrise.Messenger;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared._Sunrise.SponsorSystem;
using Content.Shared.Administration;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems;

public partial class BwoinkSystem
{
    private string FormatPlayerNameWithSponsors(AdminData? senderAdmin, ICommonSession senderSession, string adminPrefix, string name)
    {
        var isActiveAdmin = senderAdmin != null;
        var displayName = isActiveAdmin ? name : senderSession.Name;
        var escapedUsername = FormattedMessage.EscapeText(displayName);

        string? sponsorTitle = null;
        string? sponsorColorHex = null;
        bool isGradient = false;

        var isSponsor = _sponsorsManager != null && _sponsorsManager.IsSponsor(senderSession.UserId);
        var isAllowedAdminBypass = isActiveAdmin && isSponsor;

        if (_sponsorsManager != null)
        {
            _sponsorsManager.TryGetOocTitle(senderSession.UserId, out sponsorTitle);
            if (_sponsorsManager.TryGetOocColor(senderSession.UserId, out var color))
                sponsorColorHex = "#" + color.Value.ToHexNoAlpha().TrimStart('#');
        }

        var selectedTitleCVar = _netConfig.GetClientCVar(senderSession.Channel, SunriseCCVars.SponsorOocTitle);
        if (selectedTitleCVar == "@none")
        {
            sponsorTitle = null;
        }
        else if (!string.IsNullOrEmpty(selectedTitleCVar))
        {
            var isAllowedTitle = false;
            if (_sponsorsManager != null && _sponsorsManager.TryGetPrototypes(senderSession.UserId, out var prototypes))
                isAllowedTitle = prototypes.Contains(selectedTitleCVar);

            if (isAllowedAdminBypass && senderAdmin != null &&
                (selectedTitleCVar == senderAdmin.Title || OocGradientHelper.TryResolveTitle(selectedTitleCVar, out _)))
            {
                isAllowedTitle = true;
            }

            if (isAllowedTitle)
                sponsorTitle = selectedTitleCVar;
        }

        if (sponsorTitle != null && OocGradientHelper.TryResolveTitle(sponsorTitle, out var resolvedTitle))
            sponsorTitle = resolvedTitle;

        var selectedColorCVar = _netConfig.GetClientCVar(senderSession.Channel, SunriseCCVars.SponsorOocColor);
        if (selectedColorCVar == "@none")
        {
            sponsorColorHex = null;
        }
        else if (!string.IsNullOrEmpty(selectedColorCVar))
        {
            if (OocGradientHelper.IsGradientId(selectedColorCVar))
            {
                isGradient = true;
            }
            else
            {
                Color? parsedColor = null;
                if (OocGradientHelper.TryResolveColor(selectedColorCVar, out var resolvedCol))
                    parsedColor = resolvedCol;
                else if (Color.TryFromHex(selectedColorCVar, out var hexCol))
                    parsedColor = hexCol;

                if (parsedColor != null)
                {
                    var isAllowedColor = false;
                    if (_sponsorsManager != null && _sponsorsManager.TryGetPrototypes(senderSession.UserId, out var prototypes))
                        isAllowedColor = prototypes.Contains(selectedColorCVar);

                    if (isAllowedAdminBypass && senderAdmin != null)
                    {
                        var adminColorHex = senderAdmin.HasFlag(AdminFlags.Adminhelp) ? "#ff0000" : "#800080";
                        if (selectedColorCVar.Equals(adminColorHex, StringComparison.OrdinalIgnoreCase) ||
                            OocGradientHelper.TryResolveColor(selectedColorCVar, out _))
                        {
                            isAllowedColor = true;
                        }
                    }

                    if (isAllowedColor)
                        sponsorColorHex = "#" + parsedColor.Value.ToHexNoAlpha().TrimStart('#');
                }
            }
        }

        if (isActiveAdmin)
        {
            if (string.IsNullOrWhiteSpace(sponsorColorHex) && !isGradient)
            {
                if (senderAdmin!.Flags == AdminFlags.Adminhelp)
                    sponsorColorHex = "purple";
                else if (senderAdmin.HasFlag(AdminFlags.Adminhelp))
                    sponsorColorHex = "red";
                else
                    sponsorColorHex = "purple";
            }
        }

        string result;
        if (isGradient && _sponsorsManager != null && ServerOocGradientHelper.TryFormatGradientName(senderSession.UserId, displayName, _sponsorsManager, _playerManager, _netConfig, _playerCacheManager, out var gradFormatted))
        {
            result = gradFormatted;
        }
        else
        {
            var titlePart = !string.IsNullOrWhiteSpace(sponsorTitle)
                ? $"\\[{FormattedMessage.EscapeText(sponsorTitle)}\\] "
                : adminPrefix;

            if (!string.IsNullOrWhiteSpace(sponsorColorHex))
                result = $"[color={sponsorColorHex}]{titlePart}{escapedUsername}[/color]";
            else
                result = $"{titlePart}{escapedUsername}";
        }

        var hasEmojiRights = (_sponsorsManager != null && _sponsorsManager.IsAllowedOocTitleEmoji(senderSession.UserId)) || isAllowedAdminBypass;
        if (hasEmojiRights)
        {
            var emoji = _netConfig.GetClientCVar(senderSession.Channel, SunriseCCVars.SponsorOocEmoji);
            if (!string.IsNullOrWhiteSpace(emoji))
            {
                var emojiId = emoji.Trim(':');
                var emojiSystem = EntityManager.System<SharedEmojiSystem>();
                if (emojiSystem.IsEmojiAllowedForPlayer(emojiId, senderSession.UserId, _sponsorsManager))
                {
                    result = $"[emoji id=\"{emojiId}\" size=50] {result}";
                }
            }
        }

        return result;
    }
}
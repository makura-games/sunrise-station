using Content.Shared._Sunrise.SponsorSystem;
using Content.Server._Sunrise.SponsorSystem;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared._Sunrise.Messenger;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Administration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Managers;

internal sealed partial class ChatManager
{
    private string FormatOocMessage(ICommonSession player, string escapedMessage, string defaultWrappedMessage, SharedEmojiSystem emojiSystem)
    {
        static string FormatTitledDisplayName(string title, string playerName)
        {
            return $"\\[{FormattedMessage.EscapeText(title)}\\] {playerName}";
        }

        var wrappedMessage = defaultWrappedMessage;

        string? sponsorTitle = null;
        Color? sponsorColor = null;

        var isActiveAdmin = _adminManager.IsAdmin(player, includeDeAdmin: false);
        var adminData = _adminManager.GetAdminData(player, includeDeAdmin: false);
        var isSponsor = _sponsorsManager != null && _sponsorsManager.IsSponsor(player.UserId);
        var isAllowedAdminBypass = isActiveAdmin && isSponsor;

        if (_sponsorsManager != null)
        {
            _sponsorsManager.TryGetOocTitle(player.UserId, out sponsorTitle);
            _sponsorsManager.TryGetOocColor(player.UserId, out sponsorColor);
        }

        var hasEmojiRights = (_sponsorsManager != null && _sponsorsManager.IsAllowedOocTitleEmoji(player.UserId)) || isAllowedAdminBypass;

        var selectedTitleCVar = _netConfigManager.GetClientCVar(player.Channel, SunriseCCVars.SponsorOocTitle);
        if (!string.IsNullOrEmpty(selectedTitleCVar) && selectedTitleCVar != "@none")
        {
            var isAllowedTitle = false;
            if (_sponsorsManager != null && _sponsorsManager.TryGetPrototypes(player.UserId, out var prototypes))
            {
                isAllowedTitle = prototypes.Contains(selectedTitleCVar);
            }
            if (isAllowedAdminBypass && adminData != null && (selectedTitleCVar == adminData.Title || OocGradientHelper.TryResolveTitle(selectedTitleCVar, out _)))
            {
                isAllowedTitle = true;
            }

            if (isAllowedTitle)
            {
                if (OocGradientHelper.TryResolveTitle(selectedTitleCVar, out var resolvedTitle))
                    sponsorTitle = resolvedTitle;
                else
                    sponsorTitle = selectedTitleCVar;
            }
        }

        var selectedColorCVar = _netConfigManager.GetClientCVar(player.Channel, SunriseCCVars.SponsorOocColor);
        var isGradient = OocGradientHelper.IsGradientId(selectedColorCVar);

        if (isAllowedAdminBypass && adminData != null)
        {
            if (string.IsNullOrEmpty(sponsorTitle) || sponsorTitle == "@none")
            {
                sponsorTitle = adminData.Title;
            }

            if ((sponsorColor == null || selectedColorCVar == "@none") && !isGradient)
            {
                var adminColorHex = adminData.HasFlag(AdminFlags.Adminhelp) ? "#ff0000" : "#800080";
                if (Color.TryFromHex(adminColorHex, out var parsedAdminColor))
                    sponsorColor = parsedAdminColor;
            }
        }

        string sponsorDisplayName;
        if (isGradient && ServerOocGradientHelper.TryFormatGradientName(player.UserId, player.Name, _sponsorsManager, _player, _netConfigManager, _playerCacheManager, out var gradFormatted))
        {
            sponsorDisplayName = gradFormatted;
            sponsorColor = null;
        }
        else
        {
            if (!string.IsNullOrEmpty(selectedColorCVar) && selectedColorCVar != "@none" && !isGradient)
            {
                var isAllowedColor = false;
                if (Color.TryFromHex(selectedColorCVar, out var parsedColor))
                {
                    if (_sponsorsManager != null && _sponsorsManager.TryGetPrototypes(player.UserId, out var prototypes))
                    {
                        isAllowedColor = prototypes.Contains(selectedColorCVar);
                    }

                    if (isAllowedAdminBypass && adminData != null)
                    {
                        var adminColorHex = adminData.HasFlag(AdminFlags.Adminhelp) ? "#ff0000" : "#800080";
                        if (selectedColorCVar.Equals(adminColorHex, StringComparison.OrdinalIgnoreCase))
                        {
                            isAllowedColor = true;
                        }
                    }

                    if (isAllowedColor)
                    {
                        sponsorColor = parsedColor;
                    }
                }
            }

            var namePart = player.Name;
            var titlePart = sponsorTitle;
            sponsorDisplayName = string.IsNullOrWhiteSpace(titlePart)
                ? namePart
                : $"\\[{titlePart}\\] {namePart}";
        }

        var selectedEmojiCVar = _netConfigManager.GetClientCVar(player.Channel, SunriseCCVars.SponsorOocEmoji);
        if (hasEmojiRights && !string.IsNullOrWhiteSpace(selectedEmojiCVar))
        {
            var emojiId = selectedEmojiCVar.Trim(':');
            if (emojiSystem.IsEmojiAllowedForPlayer(emojiId, player.UserId, _sponsorsManager))
            {
                sponsorDisplayName = $"[emoji id=\"{emojiId}\" size=50] {sponsorDisplayName}";
            }
        }

        if (sponsorColor != null)
        {
            wrappedMessage = Loc.GetString("chat-manager-send-ooc-sponsor-wrap-message",
                ("sponsorColor", sponsorColor.Value.ToHex()),
                ("playerName", sponsorDisplayName),
                ("message", escapedMessage));
        }
        else if (sponsorDisplayName != player.Name)
        {
            wrappedMessage = Loc.GetString("chat-manager-send-ooc-wrap-message",
                ("playerName", sponsorDisplayName),
                ("message", escapedMessage));
        }
        else if (_netConfigManager.GetClientCVar(player.Channel, CCVars.ShowOocPatronColor) &&
                 player.Channel.UserData.PatronTier is { } patron &&
                 PatronOocColors.TryGetValue(patron, out var patronColor))
        {
            wrappedMessage = Loc.GetString("chat-manager-send-ooc-patron-wrap-message",
                ("patronColor", patronColor),
                ("playerName", player.Name),
                ("message", escapedMessage));
        }

        var adminTitle = _adminManager.GetAdminData(player)?.Title;
        if (!string.IsNullOrWhiteSpace(adminTitle) && sponsorDisplayName == player.Name && sponsorColor == null)
        {
            wrappedMessage = Loc.GetString("chat-manager-send-ooc-wrap-message",
                ("playerName", FormatTitledDisplayName(adminTitle, player.Name)),
                ("message", escapedMessage));
        }

        return wrappedMessage;
    }
}

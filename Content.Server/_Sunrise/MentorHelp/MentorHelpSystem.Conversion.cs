using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server._Sunrise.Messenger;
using Content.Shared._Sunrise.MentorHelp;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Administration;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using Content.Shared._Sunrise.SponsorSystem;
using Content.Server._Sunrise.SponsorSystem;

namespace Content.Server._Sunrise.MentorHelp;

public sealed partial class MentorHelpSystem
{
    private async Task<List<MentorHelpTicketData>> GetTicketDataListAsync(List<MentorHelpTicket> tickets)
    {
        var userIds = new HashSet<Guid>();

        foreach (var ticket in tickets)
        {
            userIds.Add(ticket.PlayerId);

            if (ticket.AssignedToUserId is { } assignedToUserId)
                userIds.Add(assignedToUserId);

            if (ticket.ClosedByUserId is { } closedByUserId)
                userIds.Add(closedByUserId);
        }

        var playerNames = await _dbManager.GetPlayerNamesBatchAsync(userIds);
        var ticketDataList = new List<MentorHelpTicketData>(tickets.Count);

        foreach (var ticket in tickets)
            ticketDataList.Add(ConvertToTicketData(ticket, playerNames));


        return ticketDataList;
    }

    private MentorHelpTicketData CreateTicketData(
        MentorHelpTicket ticket,
        string playerName,
        string? assignedToName,
        string? closedByName)
    {
        return new MentorHelpTicketData
        {
            Id = ticket.Id,
            PlayerId = new NetUserId(ticket.PlayerId),
            PlayerEntity = GetPlayerEntity(ticket.PlayerId),
            PlayerName = playerName,
            AssignedToUserId = ToNetUserId(ticket.AssignedToUserId),
            AssignedToName = assignedToName,
            Subject = ticket.Subject,
            Status = ticket.Status,
            CreatedAt = ticket.CreatedAt.DateTime,
            UpdatedAt = ticket.UpdatedAt.DateTime,
            ClosedAt = ticket.ClosedAt?.DateTime,
            ClosedByUserId = ToNetUserId(ticket.ClosedByUserId),
            ClosedByName = closedByName,
            RoundId = ticket.RoundId,
            HasUnreadMessages = false
        };
    }

    private NetEntity? GetPlayerEntity(Guid userId)
    {
        var netUserId = new NetUserId(userId);

        if (!_playerManager.TryGetSessionById(netUserId, out var session))
            return null;

        if (session.AttachedEntity is not { Valid: true } attachedEntity)
            return null;

        return GetNetEntity(attachedEntity);
    }

    private async Task<List<MentorHelpMessageData>> GetTicketMessagesDataAsync(int ticketId, bool includeStaffOnly)
    {
        var messages = await GetOrderedTicketMessagesAsync(ticketId);
        return await ConvertToMessageDataListAsync(messages, includeStaffOnly);
    }

    private async Task<List<MentorHelpMessageData>> ConvertToMessageDataListAsync(
        IEnumerable<MentorHelpMessage> messages,
        bool includeStaffOnly)
    {
        var messageDatas = new List<MentorHelpMessageData>();

        foreach (var message in messages)
        {
            if (!includeStaffOnly && message.IsStaffOnly)
                continue;

            messageDatas.Add(await ConvertToMessageDataAsync(message));
        }

        return messageDatas;
    }

    private async Task<List<MentorHelpMessage>> GetOrderedTicketMessagesAsync(int ticketId)
    {
        var messages = await _dbManager.GetMentorHelpMessagesByTicketAsync(ticketId);
        return [.. messages.OrderBy(message => message.SentAt)];
    }

    private async Task<(string Username, AdminData? AdminData)> ResolveMessageSenderContextAsync(NetUserId senderUserId)
    {
        AdminData? senderAdminData = null;
        string? username = null;

        if (_playerManager.TryGetSessionById(senderUserId, out var senderSession))
        {
            senderAdminData = _adminManager.GetAdminData(senderSession);
            username = senderSession.Name;
        }
        else
        {
            var loadedAdminData = await _adminManager.LoadAdminData(senderUserId);
            if (loadedAdminData is not null)
                senderAdminData = loadedAdminData.Value.dat;
        }

        username ??= await GetStoredPlayerNameAsync(senderUserId);
        username ??= Loc.GetString("mentor-help-unknown-user");

        return (username, senderAdminData);
    }

    private string FormatMessageSender(string username, NetUserId senderUserId, AdminData? senderAdminData)
    {
        var escapedUsername = FormattedMessage.EscapeText(username);

        string? sponsorTitle = null;
        string? sponsorColorHex = null;
        bool isGradient = false;
        bool explicitNoneTitle = false;
        bool explicitNoneColor = false;

        var isActiveAdmin = senderAdminData != null;
        var isSponsor = _sponsorsManager != null && _sponsorsManager.IsSponsor(senderUserId);
        var isAllowedAdminBypass = isActiveAdmin && isSponsor;

        if (_sponsorsManager != null)
        {
            _sponsorsManager.TryGetOocTitle(senderUserId, out sponsorTitle);
            if (_sponsorsManager.TryGetOocColor(senderUserId, out var color))
                sponsorColorHex = "#" + color.Value.ToHexNoAlpha().TrimStart('#');
        }

        if (_playerManager.TryGetSessionById(senderUserId, out var session))
        {
            var selectedTitleCVar = _netConfig.GetClientCVar(session.Channel, SunriseCCVars.SponsorOocTitle);
            if (selectedTitleCVar == "@none")
            {
                explicitNoneTitle = true;
                sponsorTitle = null;
            }
            else if (!string.IsNullOrEmpty(selectedTitleCVar))
            {
                var isAllowedTitle = false;
                if (_sponsorsManager != null && _sponsorsManager.TryGetPrototypes(senderUserId, out var prototypes))
                    isAllowedTitle = prototypes.Contains(selectedTitleCVar);

                if (isAllowedAdminBypass && senderAdminData != null &&
                    (selectedTitleCVar == senderAdminData.Title || OocGradientHelper.TryResolveTitle(selectedTitleCVar, out _)))
                {
                    isAllowedTitle = true;
                }

                if (isAllowedTitle)
                    sponsorTitle = selectedTitleCVar;
            }

            var selectedColorCVar = _netConfig.GetClientCVar(session.Channel, SunriseCCVars.SponsorOocColor);
            if (selectedColorCVar == "@none")
            {
                explicitNoneColor = true;
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
                        if (_sponsorsManager != null && _sponsorsManager.TryGetPrototypes(senderUserId, out var prototypes))
                            isAllowedColor = prototypes.Contains(selectedColorCVar);

                        if (isAllowedAdminBypass && senderAdminData != null)
                        {
                            var adminColorHex = senderAdminData.HasFlag(AdminFlags.Adminhelp) ? "#ff0000" : "#800080";
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
        }

        if (sponsorTitle != null && OocGradientHelper.TryResolveTitle(sponsorTitle, out var resolvedTitle))
            sponsorTitle = resolvedTitle;

        if (isActiveAdmin)
        {
            if (!explicitNoneTitle && string.IsNullOrWhiteSpace(sponsorTitle) &&
                _config.GetCVar(SunriseCCVars.MentorHelpAdminPrefixEnabled) &&
                senderAdminData?.Title is { } adminTitle)
            {
                sponsorTitle = adminTitle;
            }

            if (!explicitNoneColor && string.IsNullOrWhiteSpace(sponsorColorHex) && !isGradient)
            {
                if (senderAdminData != null && senderAdminData.HasFlag(AdminFlags.Mentor) && senderAdminData.Flags == AdminFlags.Mentor)
                    sponsorColorHex = "purple";
                else if (senderAdminData != null && senderAdminData.HasFlag(AdminFlags.Adminhelp))
                    sponsorColorHex = "red";
                else
                    sponsorColorHex = "purple";
            }
        }

        string result;
        if (isGradient && _sponsorsManager != null && ServerOocGradientHelper.TryFormatGradientName(senderUserId, username, _sponsorsManager, _playerManager, _netConfig, _playerCacheManager, out var gradFormatted))
        {
            result = gradFormatted;
        }
        else
        {
            var titlePart = string.IsNullOrWhiteSpace(sponsorTitle) ? string.Empty : $"\\[{FormattedMessage.EscapeText(sponsorTitle)}\\] ";

            if (!string.IsNullOrWhiteSpace(sponsorColorHex))
                result = $"[color={sponsorColorHex}]{titlePart}{escapedUsername}[/color]";
            else
                result = $"{titlePart}{escapedUsername}";
        }

        var hasEmojiRights = (_sponsorsManager != null && _sponsorsManager.IsAllowedOocTitleEmoji(senderUserId)) || isAllowedAdminBypass;
        if (hasEmojiRights && _playerManager.TryGetSessionById(senderUserId, out var emojiSession))
        {
            var emoji = _netConfig.GetClientCVar(emojiSession.Channel, SunriseCCVars.SponsorOocEmoji);
            if (!string.IsNullOrWhiteSpace(emoji))
            {
                var emojiId = emoji.Trim(':');
                var emojiSystem = EntityManager.System<EmojiSystem>();
                if (emojiSystem.IsEmojiAllowedForPlayer(emojiId, senderUserId, _sponsorsManager))
                {
                    result = $"[emoji id=\"{emojiId}\" size=50] {result}";
                }
            }
        }

        return result;
    }

    private async Task<string?> GetOptionalPlayerNameAsync(Guid? userId)
    {
        if (userId is not { } value)
            return null;

        return await GetPlayerNameAsync(value);
    }

    private async Task<string?> GetStoredPlayerNameAsync(NetUserId userId)
    {
        var playerData = await _dbManager.GetPlayerRecordByUserId(userId);
        return string.IsNullOrWhiteSpace(playerData?.LastSeenUserName)
            ? null
            : playerData.LastSeenUserName;
    }

    private static string? TryGetPlayerName(IReadOnlyDictionary<Guid, string> playerNames, Guid? userId)
    {
        return userId is { } value && playerNames.TryGetValue(value, out var playerName)
            ? playerName
            : null;
    }

    private static NetUserId? ToNetUserId(Guid? userId)
    {
        return userId is { } value
            ? new NetUserId(value)
            : null;
    }
}
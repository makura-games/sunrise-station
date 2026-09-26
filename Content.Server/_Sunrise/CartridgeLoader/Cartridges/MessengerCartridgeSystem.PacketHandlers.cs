using System.Linq;
using Content.Server._Sunrise.Messenger;
using Content.Shared.CartridgeLoader;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using DeviceNetworkPacketEvent = Content.Shared.DeviceNetwork.Events.DeviceNetworkPacketEvent<Content.Shared._Sunrise.DeviceNetwork.SunriseNetworkPayload>;
using Content.Shared._Sunrise.Messenger;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.PDA.Ringer;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.CartridgeLoader.Cartridges;

/// <summary>
/// Часть системы картриджа мессенджера, отвечающая за обработку входящих пакетов
/// </summary>
public sealed partial class MessengerCartridgeSystem
{
    private void OnPacketReceived(Entity<MessengerCartridgeComponent> ent, ref CartridgeRelayedEvent<DeviceNetworkPacketEvent> args)
    {
        var (uid, component) = ent;
        var packet = args.Args;

        if (!packet.Data.TryGetValue(DeviceNetworkConstants.Command, out string? command))
        {
            return;
        }

        EntityUid loaderUid = args.Loader;
        if (loaderUid == EntityUid.Invalid)
        {
            Sawmill.Warning($"Packet received but LoaderUid is invalid");
            return;
        }

        switch (command)
        {
            case MessengerCommands.CmdUserRegistered:
                HandleUserRegistered(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdUsersList:
                HandleUsersList(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdGroupsList:
                HandleGroupsList(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdMessagesList:
                HandleMessagesList(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdMessageReceived:
                HandleMessageReceived(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdGroupCreated:
                HandleGroupCreated(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdUserAddedToGroup:
                HandleUserAddedToGroup(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdInviteReceived:
                HandleInviteReceived(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdInvitesList:
                HandleInvitesList(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdUserLeftGroup:
                HandleUserLeftGroup(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdGroupDeleted:
                HandleGroupDeleted(uid, component, packet, loaderUid);
                break;
            case MessengerCommands.CmdMessageDeleted:
                HandleMessageDeleted(uid, component, packet, loaderUid);
                break;
            default:
                Sawmill.Warning($"Unknown command received: {command}");
                break;
        }
    }

    private void HandleUserRegistered(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (!packet.Data.TryGetValue("user_id", out string? userId))
        {
            Sawmill.Warning($"UserRegistered packet missing user_id");
            return;
        }

        Sawmill.Info($"User registered successfully: {userId}");
        component.UserId = userId;
        component.IsRegistered = true;
        component.LastRegistrationAttempt = null;

        if (TryComp<DeviceNetworkComponent>(loaderUid, out var deviceNetwork))
        {
            RequestUsers(uid, component, loaderUid, deviceNetwork);
            RequestGroups(uid, component, loaderUid, deviceNetwork);
        }

        UpdateUiState(uid, loaderUid, component);
    }

    private void HandleUsersList(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent args, EntityUid loaderUid)
    {
        if (!args.Data.TryGetValue("users", out List<Dictionary<string, object>>? usersData))
            return;

        var users = new List<MessengerUser>();
        foreach (var userData in usersData)
        {
            if (!userData.TryGetValue("user_id", out object? userIdObj) ||
                !userData.TryGetValue("user_name", out object? userNameObj))
                continue;

            var userId = userIdObj?.ToString();
            var userName = userNameObj?.ToString();

            if (userId == null || userName == null)
                continue;

            userData.TryGetValue("job_title", out object? jobTitleObj);
            userData.TryGetValue("department_id", out object? departmentIdObj);
            userData.TryGetValue("department_ids", out object? departmentIdsObj);
            userData.TryGetValue("job_icon_id", out object? jobIconIdObj);

            var jobTitle = jobTitleObj?.ToString();
            var departmentId = departmentIdObj?.ToString();
            var departmentIds = new List<string>();
            if (departmentIdsObj is List<object> deptList)
            {
                departmentIds.AddRange(deptList.Select(d => d.ToString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            else if (departmentIdsObj is List<string> deptStringList)
            {
                departmentIds.AddRange(deptStringList.Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            if (departmentIds.Count == 0 && departmentId != null && !string.IsNullOrWhiteSpace(departmentId))
            {
                departmentIds.Add(departmentId);
            }

            ProtoId<JobIconPrototype>? jobIconId = null;
            if (jobIconIdObj != null && !string.Empty.Equals(jobIconIdObj.ToString()))
            {
                jobIconId = new ProtoId<JobIconPrototype>(jobIconIdObj.ToString()!);
            }

            users.Add(new MessengerUser(userId, userName, jobTitle, departmentIds, jobIconId));
        }

        component.Users = users;
        UpdateUiState(uid, loaderUid, component);
    }

    private void HandleGroupsList(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (!packet.Data.TryGetValue("groups", out List<Dictionary<string, object>>? groupsData))
            return;

        Dictionary<string, int>? serverUnreadCounts = null;
        if (packet.Data.TryGetValue("unread_counts", out object? unreadCountsObj))
        {
            if (unreadCountsObj is Dictionary<string, object> unreadCountsDict)
            {
                serverUnreadCounts = new Dictionary<string, int>();
                foreach (var (chatId, countObj) in unreadCountsDict)
                {
                    if (int.TryParse(countObj?.ToString(), out var count))
                    {
                        serverUnreadCounts[chatId] = count;
                    }
                }
            }
        }

        var groups = new List<MessengerGroup>();
        foreach (var groupData in groupsData)
        {
            if (!groupData.TryGetValue("group_id", out object? groupIdObj) ||
                !groupData.TryGetValue("group_name", out object? groupNameObj))
                continue;

            var groupId = groupIdObj?.ToString();
            var groupName = groupNameObj?.ToString();

            if (groupId == null || groupName == null)
                continue;

            List<string>? membersList = null;
            if (groupData.TryGetValue("members", out object? membersObj))
            {
                if (membersObj is List<object> membersObjList)
                {
                    membersList = membersObjList.Select(m => m?.ToString() ?? string.Empty).Where(m => !string.IsNullOrEmpty(m)).ToList();
                }
                else if (membersObj is List<string> membersStringList)
                {
                    membersList = membersStringList;
                }
                else if (membersObj is IEnumerable<object> membersEnumerable)
                {
                    membersList = membersEnumerable.Select(m => m?.ToString() ?? string.Empty).Where(m => !string.IsNullOrEmpty(m)).ToList();
                }
            }

            groupData.TryGetValue("group_type", out object? groupTypeObj);
            groupData.TryGetValue("auto_group_prototype_id", out object? autoGroupPrototypeIdObj);
            groupData.TryGetValue("owner_id", out object? ownerIdObj);

            var groupType = MessengerGroupType.UserCreated;
            if (groupTypeObj != null && int.TryParse(groupTypeObj.ToString(), out var typeInt))
            {
                groupType = (MessengerGroupType)typeInt;
            }

            var autoGroupPrototypeId = autoGroupPrototypeIdObj?.ToString();
            var ownerId = ownerIdObj?.ToString();

            groups.Add(new MessengerGroup(groupId, groupName, new HashSet<string>(membersList ?? new List<string>()), groupType, autoGroupPrototypeId, ownerId));
        }

        component.Groups = groups;

        if (serverUnreadCounts != null)
        {
            foreach (var (chatId, count) in serverUnreadCounts)
            {
                component.ServerUnreadCounts[chatId] = count;
            }
        }

        UpdateUiState(uid, loaderUid, component);
    }

    private void HandleMessagesList(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (!packet.Data.TryGetValue("messages", out List<Dictionary<string, object>>? messagesData))
            return;

        var isStatusUpdate = packet.Data.TryGetValue("chat_id", out object? updateChatIdObj);
        var updateChatId = updateChatIdObj?.ToString();

        var chatId = isStatusUpdate ? updateChatId : component.LastRequestedChatId;
        if (string.IsNullOrEmpty(chatId))
        {
            if (messagesData.Count > 0)
            {
                var firstMessage = messagesData[0];
                firstMessage.TryGetValue("group_id", out object? groupIdObj);
                firstMessage.TryGetValue("recipient_id", out object? recipientIdObj);

                var groupId = groupIdObj?.ToString();
                var recipientId = recipientIdObj?.ToString();

                if (!string.IsNullOrEmpty(groupId))
                {
                    chatId = groupId;
                }
                else if (!string.IsNullOrEmpty(recipientId) && !string.IsNullOrEmpty(component.UserId))
                {
                    var ids = new[] { recipientId, component.UserId }.OrderBy(x => x).ToArray();
                    chatId = $"personal_{ids[0]}_{ids[1]}";
                }
            }
        }

        var messages = new List<MessengerMessage>();
        foreach (var messageData in messagesData)
        {
            if (!messageData.TryGetValue("sender_id", out object? senderIdObj) ||
                !messageData.TryGetValue("sender_name", out object? senderNameObj) ||
                !messageData.TryGetValue("content", out object? contentObj) ||
                !messageData.TryGetValue("timestamp", out object? timestampObj))
                continue;

            var senderId = senderIdObj?.ToString();
            var senderName = senderNameObj?.ToString();
            var content = contentObj?.ToString();

            if (senderId == null || senderName == null || content == null)
                continue;

            if (!double.TryParse(timestampObj?.ToString(), out var timestampSeconds))
                continue;

            messageData.TryGetValue("group_id", out object? groupIdObj);
            messageData.TryGetValue("recipient_id", out object? recipientIdObj);
            messageData.TryGetValue("is_read", out object? isReadObj);
            messageData.TryGetValue("message_id", out object? messageIdObj);
            messageData.TryGetValue("sender_job_icon_id", out object? senderJobIconIdObj);
            messageData.TryGetValue("image_path", out object? imagePathObj);

            var groupId = groupIdObj?.ToString();
            var recipientId = recipientIdObj?.ToString();

            var isRead = false;
            if (isReadObj != null && bool.TryParse(isReadObj.ToString(), out var isReadValue))
            {
                isRead = isReadValue;
            }

            var messageId = 0L;
            if (messageIdObj != null && long.TryParse(messageIdObj.ToString(), out var messageIdValue))
            {
                messageId = messageIdValue;
            }

            ProtoId<JobIconPrototype>? senderJobIconId = null;
            if (senderJobIconIdObj != null && !string.IsNullOrEmpty(senderJobIconIdObj.ToString()))
            {
                senderJobIconId = new ProtoId<JobIconPrototype>(senderJobIconIdObj.ToString()!);
            }

            var imagePath = imagePathObj?.ToString();
            if (string.IsNullOrWhiteSpace(imagePath))
                imagePath = null;

            var timestamp = TimeSpan.FromSeconds(timestampSeconds);
            messages.Add(new MessengerMessage(senderId, senderName, content, timestamp, groupId, recipientId, isRead, messageId, senderJobIconId, imagePath));
        }

        if (!string.IsNullOrEmpty(chatId) && messages.Count >= 0)
        {
            if (component.MessageHistory.TryGetValue(chatId, out var existingMessages))
            {
                var existingDict = existingMessages
                    .GroupBy(m => m.MessageId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var newMessage in messages)
                {
                    if (newMessage.MessageId > 0 && existingDict.TryGetValue(newMessage.MessageId, out var matchingList))
                    {
                        var matchingMessage = matchingList.FirstOrDefault();
                        if (matchingMessage != null)
                        {
                            if (matchingMessage.IsRead != newMessage.IsRead)
                            {
                                matchingMessage.IsRead = newMessage.IsRead;
                            }
                        }
                    }
                    else if (newMessage.MessageId == 0)
                    {
                        var matchingMessage = existingMessages.FirstOrDefault(m =>
                            m.MessageId == 0 &&
                            Math.Abs(m.Timestamp.TotalSeconds - newMessage.Timestamp.TotalSeconds) < 0.001 &&
                            m.SenderId == newMessage.SenderId &&
                            m.Content == newMessage.Content &&
                            m.GroupId == newMessage.GroupId &&
                            m.RecipientId == newMessage.RecipientId);

                        if (matchingMessage != null)
                        {
                            if (matchingMessage.IsRead != newMessage.IsRead)
                            {
                                matchingMessage.IsRead = newMessage.IsRead;
                            }
                        }
                    }
                }

                var newMessages = new List<MessengerMessage>();
                foreach (var newMsg in messages)
                {
                    bool isDuplicate = false;

                    if (newMsg.MessageId > 0)
                    {
                        if (existingDict.TryGetValue(newMsg.MessageId, out var msgList) && msgList.Count > 0)
                        {
                            isDuplicate = true;
                        }
                    }
                    else
                    {
                        isDuplicate = existingMessages.Any(m =>
                            m.MessageId == 0 &&
                            Math.Abs(m.Timestamp.TotalSeconds - newMsg.Timestamp.TotalSeconds) < 0.001 &&
                            m.SenderId == newMsg.SenderId &&
                            m.Content == newMsg.Content &&
                            m.GroupId == newMsg.GroupId &&
                            m.RecipientId == newMsg.RecipientId);
                    }

                    if (!isDuplicate)
                    {
                        newMessages.Add(newMsg);
                    }
                }

                if (newMessages.Count > 0)
                {
                    existingMessages.AddRange(newMessages);
                    existingMessages = existingMessages.OrderBy(m => m.MessageId)
                        .ThenBy(m => m.SenderId)
                        .ThenBy(m => m.Content)
                        .ToList();
                }
                component.MessageHistory[chatId] = existingMessages;
            }
            else
                component.MessageHistory[chatId] = messages.OrderBy(m => m.MessageId).ThenBy(m => m.SenderId).ThenBy(m => m.Content).ToList();
        }

        UpdateUiState(uid, loaderUid, component);
    }

    private void HandleMessageReceived(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (!packet.Data.TryGetValue("sender_id", out object? senderIdObj) ||
            !packet.Data.TryGetValue("sender_name", out object? senderNameObj) ||
            !packet.Data.TryGetValue("content", out object? contentObj) ||
            !packet.Data.TryGetValue("timestamp", out object? timestampObj))
            return;

        var senderId = senderIdObj?.ToString();
        var senderName = senderNameObj?.ToString();
        var content = contentObj?.ToString();

        if (senderId == null || senderName == null || content == null)
            return;

        if (!double.TryParse(timestampObj?.ToString(), out var timestampSeconds))
            return;

        packet.Data.TryGetValue("group_id", out object? groupIdObj);
        packet.Data.TryGetValue("recipient_id", out object? recipientIdObj);
        packet.Data.TryGetValue("is_read", out object? isReadObj);
        packet.Data.TryGetValue("message_id", out object? messageIdObj);
        packet.Data.TryGetValue("sender_job_icon_id", out object? senderJobIconIdObj);
        packet.Data.TryGetValue("image_path", out object? imagePathObj);

        var groupId = groupIdObj?.ToString();
        var recipientId = recipientIdObj?.ToString();

        var isRead = false;
        if (isReadObj != null && bool.TryParse(isReadObj.ToString(), out var isReadValue))
        {
            isRead = isReadValue;
        }

        var messageId = 0L;
        if (messageIdObj != null && long.TryParse(messageIdObj.ToString(), out var messageIdValue))
        {
            messageId = messageIdValue;
        }

        ProtoId<JobIconPrototype>? senderJobIconId = null;
        if (senderJobIconIdObj != null && !string.IsNullOrEmpty(senderJobIconIdObj.ToString()))
        {
            senderJobIconId = new ProtoId<JobIconPrototype>(senderJobIconIdObj.ToString()!);
        }

        var imagePath = imagePathObj?.ToString();
        if (string.IsNullOrWhiteSpace(imagePath))
            imagePath = null;

        var timestamp = TimeSpan.FromSeconds(timestampSeconds);
        var message = new MessengerMessage(senderId, senderName, content, timestamp, groupId, recipientId, isRead, messageId, senderJobIconId, imagePath);

        string chatId;
        if (!string.IsNullOrEmpty(groupId))
        {
            chatId = groupId;
        }
        else if (!string.IsNullOrEmpty(senderId) && !string.IsNullOrEmpty(component.UserId))
        {
            string otherUserId;
            if (component.UserId == senderId)
            {
                if (string.IsNullOrEmpty(recipientId))
                {
                    otherUserId = senderId;
                }
                else
                {
                    otherUserId = recipientId;
                }
            }
            else
            {
                otherUserId = senderId;
            }

            var ids = new[] { component.UserId, otherUserId }.OrderBy(x => x).ToArray();
            chatId = $"personal_{ids[0]}_{ids[1]}";
        }
        else
        {
            chatId = senderId;
        }

        if (!component.MessageHistory.TryGetValue(chatId, out var history))
        {
            history = new List<MessengerMessage>();
            component.MessageHistory[chatId] = history;
        }

        MessengerMessage? existingMessage = null;

        if (message.MessageId > 0)
        {
            existingMessage = history.FirstOrDefault(m => m.MessageId == message.MessageId);
        }
        else
        {
            existingMessage = history.FirstOrDefault(m =>
                m.MessageId == 0 &&
                Math.Abs(m.Timestamp.TotalSeconds - message.Timestamp.TotalSeconds) < 0.001 &&
                m.SenderId == message.SenderId &&
                m.Content == message.Content &&
                m.GroupId == message.GroupId &&
                m.RecipientId == message.RecipientId);
        }

        if (existingMessage == null)
        {
            history.Add(message);
            history = history.OrderBy(m => m.MessageId)
                .ThenBy(m => m.SenderId)
                .ThenBy(m => m.Content)
                .ToList();
            component.MessageHistory[chatId] = history;
        }
        else
        {
            existingMessage.IsRead = message.IsRead;
        }

        var isGroupChat = chatId == "common" || chatId.StartsWith("dept_") || chatId.StartsWith("group_");
        var isMuted = isGroupChat
            ? component.MutedGroupChats.Contains(chatId)
            : component.MutedPersonalChats.Contains(chatId);

        var isSender = component.UserId == senderId;
        var isChatOpen = component.LastRequestedChatId == chatId;

        if (!isSender && !isChatOpen && isGroupChat)
        {
            component.ServerUnreadCounts.TryGetValue(chatId, out var currentCount);
            component.ServerUnreadCounts[chatId] = currentCount + 1;
        }

        if (!isMuted && !isSender)
        {
            if (TryComp<RingerComponent>(loaderUid, out var ringer))
                _ringer.RingerPlayRingtone(loaderUid);

            string notificationMessage;
            if (isGroupChat)
            {
                var groupName = component.Groups.FirstOrDefault(g => g.GroupId == chatId)?.Name ?? chatId ?? string.Empty;
                notificationMessage = Loc.GetString("messenger-group-notification-message", ("name", groupName));
            }
            else
            {
                notificationMessage = Loc.GetString("messenger-notification-message", ("name", senderName ?? string.Empty));
            }

            _cartridgeLoader.SendNotification(loaderUid, Loc.GetString("messenger-program-name"), notificationMessage);
        }

        UpdateUiState(uid, loaderUid, component);

        if (TryComp<DeviceNetworkComponent>(loaderUid, out var deviceNetwork))
        {
            RequestUsers(uid, component, loaderUid, deviceNetwork);
            RequestGroups(uid, component, loaderUid, deviceNetwork);
        }
    }

    private void HandleInviteReceived(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (!packet.Data.TryGetValue("group_id", out object? groupIdObj) ||
            !packet.Data.TryGetValue("group_name", out object? groupNameObj) ||
            !packet.Data.TryGetValue("inviter_id", out object? inviterIdObj) ||
            !packet.Data.TryGetValue("inviter_name", out object? inviterNameObj) ||
            !packet.Data.TryGetValue("created_at", out object? createdAtObj))
            return;

        var groupId = groupIdObj?.ToString();
        var groupName = groupNameObj?.ToString();
        var inviterId = inviterIdObj?.ToString();
        var inviterName = inviterNameObj?.ToString();

        if (groupId == null || groupName == null || inviterId == null || inviterName == null)
            return;

        if (!double.TryParse(createdAtObj?.ToString(), out var createdAtSeconds))
            return;

        var createdAt = TimeSpan.FromSeconds(createdAtSeconds);

        var invite = new MessengerGroupInvite(groupId, groupName, inviterId, inviterName, component.UserId ?? string.Empty, createdAt);

        var isNewInvite = !component.ActiveInvites.Any(inv => inv.GroupId == groupId && inv.InviterId == inviterId);

        if (isNewInvite)
        {
            component.ActiveInvites.Add(invite);
            _cartridgeLoader.SendNotification(loaderUid, Loc.GetString("messenger-program-name"), Loc.GetString("messenger-invite-notification-message", ("name", groupName ?? string.Empty)));
        }

        UpdateUiState(uid, loaderUid, component);
    }

    private void HandleInvitesList(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (!packet.Data.TryGetValue("invites", out List<Dictionary<string, object>>? invitesData))
            return;

        component.ActiveInvites.Clear();

        foreach (var inviteData in invitesData)
        {
            if (!inviteData.TryGetValue("group_id", out object? groupIdObj) ||
                !inviteData.TryGetValue("group_name", out object? groupNameObj) ||
                !inviteData.TryGetValue("inviter_id", out object? inviterIdObj) ||
                !inviteData.TryGetValue("inviter_name", out object? inviterNameObj) ||
                !inviteData.TryGetValue("created_at", out object? createdAtObj))
                continue;

            var groupId = groupIdObj?.ToString();
            var groupName = groupNameObj?.ToString();
            var inviterId = inviterIdObj?.ToString();
            var inviterName = inviterNameObj?.ToString();

            if (groupId == null || groupName == null || inviterId == null || inviterName == null)
                continue;

            if (!double.TryParse(createdAtObj?.ToString(), out var createdAtSeconds))
                continue;

            var createdAt = TimeSpan.FromSeconds(createdAtSeconds);

            var invite = new MessengerGroupInvite(groupId, groupName, inviterId, inviterName, component.UserId ?? string.Empty, createdAt);
            component.ActiveInvites.Add(invite);
        }

        UpdateUiState(uid, loaderUid, component);
    }

    private void HandleGroupCreated(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (TryComp<DeviceNetworkComponent>(loaderUid, out var deviceNetwork))
            RequestGroups(uid, component, loaderUid, deviceNetwork);
    }

    private void HandleUserAddedToGroup(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (!packet.Data.TryGetValue("user_id", out object? addedUserIdObj) ||
            !packet.Data.TryGetValue("group_id", out object? groupIdObj))
            return;

        var addedUserId = addedUserIdObj?.ToString();
        var groupId = groupIdObj?.ToString();

        if (addedUserId == component.UserId && !string.IsNullOrEmpty(groupId))
        {
            var group = component.Groups.FirstOrDefault(g => g.GroupId == groupId);
            if (group != null)
            {
                _cartridgeLoader.SendNotification(loaderUid, Loc.GetString("messenger-program-name"), Loc.GetString("messenger-invite-notification-message", ("name", group.Name)));
            }
        }

        if (TryComp<DeviceNetworkComponent>(loaderUid, out var deviceNetwork))
            RequestGroups(uid, component, loaderUid, deviceNetwork);
    }

    private void HandleUserLeftGroup(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (!packet.Data.TryGetValue("group_id", out object? groupIdObj) ||
            !packet.Data.TryGetValue("user_id", out object? userIdObj))
            return;

        var groupId = groupIdObj?.ToString();
        var userId = userIdObj?.ToString();

        if (string.IsNullOrEmpty(groupId))
            return;

        if (userId == component.UserId)
        {
            component.Groups.RemoveAll(g => g.GroupId == groupId);
            component.MessageHistory.Remove(groupId);
            component.MutedGroupChats.Remove(groupId);
            component.ServerUnreadCounts.Remove(groupId);
        }
        else
        {
            var group = component.Groups.FirstOrDefault(g => g.GroupId == groupId);
            if (group != null && !string.IsNullOrEmpty(userId))
            {
                group.Members.Remove(userId);
            }
        }

        if (TryComp<DeviceNetworkComponent>(loaderUid, out var deviceNetwork))
            RequestGroups(uid, component, loaderUid, deviceNetwork);

        UpdateUiState(uid, loaderUid, component);
    }

    private void HandleGroupDeleted(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (!packet.Data.TryGetValue("group_id", out object? groupIdObj))
            return;

        var groupId = groupIdObj?.ToString();
        if (string.IsNullOrEmpty(groupId))
            return;

        component.Groups.RemoveAll(g => g.GroupId == groupId);
        component.MessageHistory.Remove(groupId);
        component.MutedGroupChats.Remove(groupId);
        component.ServerUnreadCounts.Remove(groupId);

        component.ActiveInvites.RemoveAll(inv => inv.GroupId == groupId);

        if (TryComp<DeviceNetworkComponent>(loaderUid, out var deviceNetwork))
            RequestGroups(uid, component, loaderUid, deviceNetwork);

        UpdateUiState(uid, loaderUid, component);
    }

    private void HandleMessageDeleted(EntityUid uid, MessengerCartridgeComponent component, DeviceNetworkPacketEvent packet, EntityUid loaderUid)
    {
        if (!packet.Data.TryGetValue("message_id", out object? messageIdObj) ||
            !packet.Data.TryGetValue("chat_id", out object? chatIdObj))
            return;

        if (!long.TryParse(messageIdObj?.ToString(), out var messageId))
            return;

        var chatId = chatIdObj?.ToString();
        if (string.IsNullOrEmpty(chatId))
            return;

        if (component.MessageHistory.TryGetValue(chatId, out var history))
        {
            history.RemoveAll(m => m.MessageId == messageId);
            if (history.Count == 0)
                component.MessageHistory.Remove(chatId);
        }

        UpdateUiState(uid, loaderUid, component);
    }
}

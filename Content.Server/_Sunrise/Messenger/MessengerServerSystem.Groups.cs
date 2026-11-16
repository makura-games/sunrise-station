using System.Linq;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using DeviceNetworkPacketEvent = Content.Shared.DeviceNetwork.Events.DeviceNetworkPacketEvent<Content.Shared._Sunrise.DeviceNetwork.SunriseNetworkPayload>;
using Content.Shared._Sunrise.Messenger;

namespace Content.Server._Sunrise.Messenger;

/// <summary>
/// Часть системы мессенджера, отвечающая за управление группами
/// </summary>
public sealed partial class MessengerServerSystem
{
    private void HandleCreateGroup(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(MessengerCommands.CmdCreateGroup, out NetworkPayload? groupData))
            return;

        if (!groupData.TryGetValue("name", out string? groupName) || string.IsNullOrWhiteSpace(groupName))
            return;

        if (!component.Users.TryGetValue(args.SenderAddress, out var creator))
            return;

        var groupId = $"group_{++component.GroupIdCounter}";
        var members = new HashSet<string> { creator.UserId };

        var group = new MessengerGroup(groupId, groupName, members, MessengerGroupType.UserCreated, null, creator.UserId);
        component.Groups[groupId] = group;

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        var membersList = new List<object>();
        foreach (var memberId in group.Members)
        {
            membersList.Add(memberId);
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdGroupCreated,
            ["group_id"] = group.GroupId,
            ["group_name"] = group.Name,
            ["group_type"] = (int)group.Type,
            ["auto_group_prototype_id"] = group.AutoGroupPrototypeId ?? string.Empty,
            ["owner_id"] = group.OwnerId ?? string.Empty,
            ["members"] = membersList
        };

        if (pdaFrequency.HasValue)
        {
            _deviceNetwork.SendPacket(uid, args.SenderAddress, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }
        else
        {
            _deviceNetwork.SendPacket(uid, args.SenderAddress, ref payload);
        }
    }

    private void HandleAddToGroup(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(MessengerCommands.CmdAddToGroup, out NetworkPayload? addData))
            return;

        if (!addData.TryGetValue("group_id", out string? groupId))
            return;

        if (!addData.TryGetValue("user_id", out string? userId))
            return;

        if (!component.Groups.TryGetValue(groupId, out var group))
            return;

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        if (group.Type == MessengerGroupType.UserCreated)
        {
            if (!component.Users.TryGetValue(args.SenderAddress, out var inviter))
                return;

            if (group.OwnerId != inviter.UserId)
                return;

            if (!component.Users.TryGetValue(userId, out var invitee))
                return;

            if (group.Members.Contains(userId))
                return;

            if (component.ActiveInvites.TryGetValue(userId, out var existingInvites))
            {
                if (existingInvites.Any(inv => inv.GroupId == groupId))
                    return;
            }

            var invite = new MessengerGroupInvite(
                groupId,
                group.Name,
                inviter.UserId,
                inviter.Name,
                userId,
                GetStationTime()
            );

            if (!component.ActiveInvites.TryGetValue(userId, out var invites))
            {
                invites = new List<MessengerGroupInvite>();
                component.ActiveInvites[userId] = invites;
            }
            invites.Add(invite);

            var invitePayload = new NetworkPayload
            {
                [DeviceNetworkConstants.Command] = MessengerCommands.CmdInviteReceived,
                ["group_id"] = groupId,
                ["group_name"] = group.Name,
                ["inviter_id"] = inviter.UserId,
                ["inviter_name"] = inviter.Name,
                ["created_at"] = invite.CreatedAt.TotalSeconds
            };

            if (pdaFrequency.HasValue)
            {
                _deviceNetwork.SendPacket(uid, userId, ref invitePayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, userId, ref invitePayload);
            }

            SendInvitesList(uid, component, userId, serverDevice, pdaFrequency);
            return;
        }

        if (group.AutoGroupPrototypeId != null)
        {
            if (ProtoMan.TryIndex<MessengerAutoGroupPrototype>(group.AutoGroupPrototypeId, out var autoGroupProto))
            {
                if (!autoGroupProto.AllowManualMemberManagement)
                    return;
            }
            else
            {
                return;
            }
        }
        else
        {
            return;
        }

        if (!component.Users.TryGetValue(args.SenderAddress, out var adder))
            return;

        if (group.OwnerId != adder.UserId)
            return;

        if (!component.Users.TryGetValue(userId, out _))
            return;

        if (group.Members.Contains(userId))
            return;

        group.Members.Add(userId);

        if (!component.Users.TryGetValue(userId, out var addedUser))
            return;

        var timestamp = GetStationTime();
        var messageText = Loc.GetString("messenger-system-user-added-by", ("adderName", adder.Name), ("userName", addedUser.Name));
        var messageId = GetNextMessageId(uid, component);
        var systemMessage = new MessengerMessage("system", Loc.GetString("messenger-system-name"), messageText, timestamp, groupId, null, false, messageId);

        if (!component.MessageHistory.TryGetValue(groupId, out var history))
        {
            history = new List<MessengerMessage>();
            component.MessageHistory[groupId] = history;
        }
        history.Add(systemMessage);
        TrimMessageHistory(history, component.MaxMessageHistory);

        var messagePayload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessageReceived,
            ["sender_id"] = "system",
            ["sender_name"] = Loc.GetString("messenger-system-name"),
            ["content"] = messageText,
            ["timestamp"] = timestamp.TotalSeconds,
            ["group_id"] = groupId,
            ["recipient_id"] = string.Empty,
            ["is_read"] = false,
            ["message_id"] = systemMessage.MessageId,
            ["image_path"] = systemMessage.ImagePath ?? string.Empty
        };

        foreach (var memberId in group.Members)
        {
            var isMemberChatOpen = component.OpenChats.TryGetValue(memberId, out var memberOpenChatId) && memberOpenChatId == groupId;

            if (!isMemberChatOpen)
            {
                if (!component.UnreadCounts.TryGetValue(memberId, out var memberUnreads))
                {
                    memberUnreads = new Dictionary<string, int>();
                    component.UnreadCounts[memberId] = memberUnreads;
                }
                memberUnreads.TryGetValue(groupId, out var currentCount);
                memberUnreads[groupId] = currentCount + 1;
            }

            if (pdaFrequency.HasValue)
            {
                _deviceNetwork.SendPacket(uid, memberId, ref messagePayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, memberId, ref messagePayload);
            }
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdUserAddedToGroup,
            ["group_id"] = groupId,
            ["user_id"] = userId
        };

        foreach (var memberId in group.Members)
        {
            if (pdaFrequency.HasValue)
            {
                _deviceNetwork.SendPacket(uid, memberId, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, memberId, ref payload);
            }
        }

        if (component.MessageHistory.TryGetValue(groupId, out var groupHistory) && groupHistory.Count > 0)
        {
            var sortedMessages = groupHistory.OrderBy(m => m.MessageId).ToList();

            var messagesData = new List<Dictionary<string, object>>();
            foreach (var msg in sortedMessages)
            {
                messagesData.Add(new Dictionary<string, object>
                {
                    ["sender_id"] = msg.SenderId,
                    ["sender_name"] = msg.SenderName,
                    ["content"] = msg.Content,
                    ["timestamp"] = msg.Timestamp.TotalSeconds,
                    ["group_id"] = msg.GroupId ?? string.Empty,
                    ["recipient_id"] = msg.RecipientId ?? string.Empty,
                    ["is_read"] = msg.IsRead,
                    ["message_id"] = msg.MessageId,
                    ["sender_job_icon_id"] = msg.SenderJobIconId?.Id ?? string.Empty,
                    ["image_path"] = msg.ImagePath ?? string.Empty
                });
            }

            var historyPayload = new NetworkPayload
            {
                [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessagesList,
                ["messages"] = messagesData,
                ["chat_id"] = groupId
            };

            if (pdaFrequency.HasValue)
            {
                _deviceNetwork.SendPacket(uid, userId, ref historyPayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, userId, ref historyPayload);
            }
        }
    }

    private void HandleRemoveFromGroup(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(MessengerCommands.CmdRemoveFromGroup, out NetworkPayload? removeData))
            return;

        if (!removeData.TryGetValue("group_id", out string? groupId))
            return;

        if (!removeData.TryGetValue("user_id", out string? userId))
            return;

        if (!component.Groups.TryGetValue(groupId, out var group))
            return;

        if (group.Type != MessengerGroupType.UserCreated)
        {
            if (group.AutoGroupPrototypeId != null)
            {
                if (ProtoMan.TryIndex<MessengerAutoGroupPrototype>(group.AutoGroupPrototypeId, out var autoGroupProto))
                {
                    if (!autoGroupProto.AllowManualMemberManagement)
                        return;
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        if (!component.Users.TryGetValue(args.SenderAddress, out var remover))
            return;

        if (group.OwnerId != remover.UserId)
            return;

        if (userId == group.OwnerId)
            return;

        if (!group.Members.Contains(userId))
            return;

        if (!component.Users.TryGetValue(userId, out var removedUser))
            return;

        group.Members.Remove(userId);

        var timestamp = GetStationTime();
        var messageText = Loc.GetString("messenger-system-user-removed-by", ("removerName", remover.Name), ("userName", removedUser.Name));
        var messageId = GetNextMessageId(uid, component);
        var systemMessage = new MessengerMessage("system", Loc.GetString("messenger-system-name"), messageText, timestamp, groupId, null, false, messageId);

        if (!component.MessageHistory.TryGetValue(groupId, out var history))
        {
            history = new List<MessengerMessage>();
            component.MessageHistory[groupId] = history;
        }
        history.Add(systemMessage);
        TrimMessageHistory(history, component.MaxMessageHistory);

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        var messagePayload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessageReceived,
            ["sender_id"] = "system",
            ["sender_name"] = Loc.GetString("messenger-system-name"),
            ["content"] = messageText,
            ["timestamp"] = timestamp.TotalSeconds,
            ["group_id"] = groupId,
            ["recipient_id"] = string.Empty,
            ["is_read"] = false,
            ["message_id"] = systemMessage.MessageId,
            ["image_path"] = systemMessage.ImagePath ?? string.Empty
        };

        foreach (var memberId in group.Members)
        {
            if (pdaFrequency.HasValue)
            {
                _deviceNetwork.SendPacket(uid, memberId, ref messagePayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, memberId, ref messagePayload);
            }
        }
    }

    /// <summary>
    /// Обрабатывает принятие приглашения в группу
    /// </summary>
    private void HandleAcceptInvite(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(MessengerCommands.CmdAcceptInvite, out NetworkPayload? acceptData))
            return;

        if (!acceptData.TryGetValue("group_id", out string? groupId))
            return;

        if (!component.Users.TryGetValue(args.SenderAddress, out var user))
            return;

        if (!component.ActiveInvites.TryGetValue(user.UserId, out var invites))
            return;

        var invite = invites.FirstOrDefault(inv => inv.GroupId == groupId);
        if (invite == null)
            return;

        if (!component.Groups.TryGetValue(groupId, out var group))
            return;

        if (group.Members.Contains(user.UserId))
        {
            invites.Remove(invite);
            if (invites.Count == 0)
                component.ActiveInvites.Remove(user.UserId);
            return;
        }

        group.Members.Add(user.UserId);

        invites.Remove(invite);
        if (invites.Count == 0)
            component.ActiveInvites.Remove(user.UserId);

        var timestamp = GetStationTime();
        var messageText = Loc.GetString("messenger-system-user-joined", ("userName", user.Name));
        var messageId = GetNextMessageId(uid, component);
        var systemMessage = new MessengerMessage("system", Loc.GetString("messenger-system-name"), messageText, timestamp, groupId, null, false, messageId);

        if (!component.MessageHistory.TryGetValue(groupId, out var history))
        {
            history = new List<MessengerMessage>();
            component.MessageHistory[groupId] = history;
        }
        history.Add(systemMessage);
        TrimMessageHistory(history, component.MaxMessageHistory);

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        var messagePayload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessageReceived,
            ["sender_id"] = "system",
            ["sender_name"] = Loc.GetString("messenger-system-name"),
            ["content"] = messageText,
            ["timestamp"] = timestamp.TotalSeconds,
            ["group_id"] = groupId,
            ["recipient_id"] = string.Empty,
            ["is_read"] = false,
            ["message_id"] = systemMessage.MessageId,
            ["image_path"] = systemMessage.ImagePath ?? string.Empty
        };

        foreach (var memberId in group.Members)
        {
            var isMemberChatOpen = component.OpenChats.TryGetValue(memberId, out var memberOpenChatId) && memberOpenChatId == groupId;

            if (!isMemberChatOpen)
            {
                if (!component.UnreadCounts.TryGetValue(memberId, out var memberUnreads))
                {
                    memberUnreads = new Dictionary<string, int>();
                    component.UnreadCounts[memberId] = memberUnreads;
                }
                memberUnreads.TryGetValue(groupId, out var currentCount);
                memberUnreads[groupId] = currentCount + 1;
            }

            if (pdaFrequency.HasValue)
            {
                _deviceNetwork.SendPacket(uid, memberId, ref messagePayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, memberId, ref messagePayload);
            }
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdUserAddedToGroup,
            ["group_id"] = groupId,
            ["user_id"] = user.UserId
        };

        foreach (var memberId in group.Members)
        {
            if (pdaFrequency.HasValue)
            {
                _deviceNetwork.SendPacket(uid, memberId, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, memberId, ref payload);
            }
        }

        if (component.MessageHistory.TryGetValue(groupId, out var groupHistory) && groupHistory.Count > 0)
        {
            var sortedMessages = groupHistory.OrderBy(m => m.MessageId).ToList();

            var messagesData = new List<Dictionary<string, object>>();
            foreach (var msg in sortedMessages)
            {
                messagesData.Add(new Dictionary<string, object>
                {
                    ["sender_id"] = msg.SenderId,
                    ["sender_name"] = msg.SenderName,
                    ["content"] = msg.Content,
                    ["timestamp"] = msg.Timestamp.TotalSeconds,
                    ["group_id"] = msg.GroupId ?? string.Empty,
                    ["recipient_id"] = msg.RecipientId ?? string.Empty,
                    ["is_read"] = msg.IsRead,
                    ["message_id"] = msg.MessageId,
                    ["sender_job_icon_id"] = msg.SenderJobIconId?.Id ?? string.Empty,
                    ["image_path"] = msg.ImagePath ?? string.Empty
                });
            }

            var historyPayload = new NetworkPayload
            {
                [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessagesList,
                ["messages"] = messagesData,
                ["chat_id"] = groupId
            };

            if (pdaFrequency.HasValue)
            {
                _deviceNetwork.SendPacket(uid, user.UserId, ref historyPayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, user.UserId, ref historyPayload);
            }
        }

        SendInvitesList(uid, component, user.UserId, serverDevice, pdaFrequency);
    }

    /// <summary>
    /// Обрабатывает отклонение приглашения в группу
    /// </summary>
    private void HandleDeclineInvite(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(MessengerCommands.CmdDeclineInvite, out NetworkPayload? declineData))
            return;

        if (!declineData.TryGetValue("group_id", out string? groupId))
            return;

        if (!component.Users.TryGetValue(args.SenderAddress, out var user))
            return;

        if (!component.ActiveInvites.TryGetValue(user.UserId, out var invites))
            return;

        var invite = invites.FirstOrDefault(inv => inv.GroupId == groupId);
        if (invite == null)
            return;

        invites.Remove(invite);
        if (invites.Count == 0)
            component.ActiveInvites.Remove(user.UserId);

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        SendInvitesList(uid, component, user.UserId, serverDevice, pdaFrequency);
    }

    /// <summary>
    /// Отправляет список активных инвайтов пользователю
    /// </summary>
    private void SendInvitesList(EntityUid uid, MessengerServerComponent component, string userId, DeviceNetworkComponent serverDevice, uint? pdaFrequency)
    {
        var invitesList = new List<Dictionary<string, object>>();

        if (component.ActiveInvites.TryGetValue(userId, out var invites))
        {
            foreach (var invite in invites)
            {
                invitesList.Add(new Dictionary<string, object>
                {
                    ["group_id"] = invite.GroupId,
                    ["group_name"] = invite.GroupName,
                    ["inviter_id"] = invite.InviterId,
                    ["inviter_name"] = invite.InviterName,
                    ["created_at"] = invite.CreatedAt.TotalSeconds
                });
            }
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdInvitesList,
            ["invites"] = invitesList
        };

        if (pdaFrequency.HasValue)
        {
            _deviceNetwork.SendPacket(uid, userId, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }
        else
        {
            _deviceNetwork.SendPacket(uid, userId, ref payload);
        }
    }

    /// <summary>
    /// Обрабатывает выход пользователя из группы
    /// </summary>
    private void HandleLeaveGroup(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(MessengerCommands.CmdLeaveGroup, out NetworkPayload? leaveData))
            return;

        if (!leaveData.TryGetValue("group_id", out string? groupId))
            return;

        if (!component.Users.TryGetValue(args.SenderAddress, out var user))
            return;

        if (!component.Groups.TryGetValue(groupId, out var group))
            return;

        if (!group.Members.Contains(user.UserId))
            return;

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        if (group.OwnerId == user.UserId && group.Type == MessengerGroupType.UserCreated)
        {
            var membersToNotify = new List<string>(group.Members);
            component.Groups.Remove(groupId);
            component.MessageHistory.Remove(groupId);

            foreach (var memberId in membersToNotify)
            {
                if (component.UnreadCounts.TryGetValue(memberId, out var memberUnreads))
                {
                    memberUnreads.Remove(groupId);
                    if (memberUnreads.Count == 0)
                        component.UnreadCounts.Remove(memberId);
                }

                if (component.OpenChats.TryGetValue(memberId, out var openChatId) && openChatId == groupId)
                {
                    component.OpenChats[memberId] = null;
                }

                var deletePayload = new NetworkPayload
                {
                    [DeviceNetworkConstants.Command] = MessengerCommands.CmdGroupDeleted,
                    ["group_id"] = groupId
                };

                if (pdaFrequency.HasValue)
                {
                    _deviceNetwork.SendPacket(uid, memberId, ref deletePayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
                }
                else
                {
                    _deviceNetwork.SendPacket(uid, memberId, ref deletePayload);
                }
            }
        }
        else
        {
            group.Members.Remove(user.UserId);

            if (group.Type == MessengerGroupType.UserCreated)
            {
                var timestamp = GetStationTime();
                var messageText = Loc.GetString("messenger-system-user-left", ("userName", user.Name));
                var messageId = GetNextMessageId(uid, component);
                var systemMessage = new MessengerMessage("system", Loc.GetString("messenger-system-name"), messageText, timestamp, groupId, null, false, messageId);

                if (!component.MessageHistory.TryGetValue(groupId, out var history))
                {
                    history = new List<MessengerMessage>();
                    component.MessageHistory[groupId] = history;
                }
                history.Add(systemMessage);
                TrimMessageHistory(history, component.MaxMessageHistory);

                var messagePayload = new NetworkPayload
                {
                    [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessageReceived,
                    ["sender_id"] = "system",
                    ["sender_name"] = Loc.GetString("messenger-system-name"),
                    ["content"] = messageText,
                    ["timestamp"] = timestamp.TotalSeconds,
                    ["group_id"] = groupId,
                    ["recipient_id"] = string.Empty,
                    ["is_read"] = false,
                    ["message_id"] = systemMessage.MessageId,
                    ["image_path"] = systemMessage.ImagePath ?? string.Empty
                };

                foreach (var memberId in group.Members)
                {
                    var isMemberChatOpen = component.OpenChats.TryGetValue(memberId, out var memberOpenChatId) && memberOpenChatId == groupId;

                    if (!isMemberChatOpen)
                    {
                        if (!component.UnreadCounts.TryGetValue(memberId, out var memberUnreads))
                        {
                            memberUnreads = new Dictionary<string, int>();
                            component.UnreadCounts[memberId] = memberUnreads;
                        }
                        memberUnreads.TryGetValue(groupId, out var currentCount);
                        memberUnreads[groupId] = currentCount + 1;
                    }

                    if (pdaFrequency.HasValue)
                    {
                        _deviceNetwork.SendPacket(uid, memberId, ref messagePayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
                    }
                    else
                    {
                        _deviceNetwork.SendPacket(uid, memberId, ref messagePayload);
                    }
                }
            }

            var payload = new NetworkPayload
            {
                [DeviceNetworkConstants.Command] = MessengerCommands.CmdUserLeftGroup,
                ["group_id"] = groupId,
                ["user_id"] = user.UserId
            };

            foreach (var memberId in group.Members)
            {
                if (pdaFrequency.HasValue)
                {
                    _deviceNetwork.SendPacket(uid, memberId, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
                }
                else
                {
                    _deviceNetwork.SendPacket(uid, memberId, ref payload);
                }
            }

            var leftPayload = new NetworkPayload
            {
                [DeviceNetworkConstants.Command] = MessengerCommands.CmdUserLeftGroup,
                ["group_id"] = groupId,
                ["user_id"] = user.UserId
            };

            if (pdaFrequency.HasValue)
            {
                _deviceNetwork.SendPacket(uid, user.UserId, ref leftPayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, user.UserId, ref leftPayload);
            }

            if (component.UnreadCounts.TryGetValue(user.UserId, out var userUnreads))
            {
                userUnreads.Remove(groupId);
                if (userUnreads.Count == 0)
                    component.UnreadCounts.Remove(user.UserId);
            }

            if (component.OpenChats.TryGetValue(user.UserId, out var userOpenChatId) && userOpenChatId == groupId)
            {
                component.OpenChats[user.UserId] = null;
            }
        }
    }
}

using System.Linq;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using DeviceNetworkPacketEvent = Content.Shared.DeviceNetwork.Events.DeviceNetworkPacketEvent<Content.Shared._Sunrise.DeviceNetwork.SunriseNetworkPayload>;
using Content.Shared._Sunrise.Messenger;

namespace Content.Server._Sunrise.Messenger;

/// <summary>
/// Часть системы мессенджера, отвечающая за обработку запросов данных
/// </summary>
public sealed partial class MessengerServerSystem
{
    private void HandleGetUsers(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        var usersList = component.Users.Values.ToList();
        var usersData = new List<Dictionary<string, object>>();

        foreach (var user in usersList)
        {
            usersData.Add(new Dictionary<string, object>
            {
                ["user_id"] = user.UserId,
                ["user_name"] = user.Name,
                ["job_title"] = user.JobTitle ?? string.Empty,
                ["department_id"] = user.DepartmentId ?? string.Empty,
                ["job_icon_id"] = user.JobIconId?.Id ?? string.Empty
            });
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdUsersList,
            ["users"] = usersData
        };

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        if (pdaFrequency.HasValue)
        {
            _deviceNetwork.SendPacket(uid, args.SenderAddress, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }
        else
        {
            _deviceNetwork.SendPacket(uid, args.SenderAddress, ref payload);
        }
    }

    private void HandleGetGroups(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        var senderUserId = args.SenderAddress;
        if (string.IsNullOrEmpty(senderUserId))
            return;

        var groupsList = component.Groups.Values.ToList();
        var groupsData = new List<Dictionary<string, object>>();

        foreach (var group in groupsList)
        {
            if (group.GroupId == "common")
            {
                if (!group.Members.Contains(senderUserId))
                    continue;
            }
            else if (group.GroupId.StartsWith("dept_"))
            {
                if (!group.Members.Contains(senderUserId))
                    continue;
            }
            else if (group.GroupId.StartsWith("group_"))
            {
                if (!group.Members.Contains(senderUserId))
                    continue;
            }

            var membersList = new List<object>();
            foreach (var memberId in group.Members)
            {
                membersList.Add(memberId);
            }

            var unreadCount = 0;
            if (component.UnreadCounts.TryGetValue(senderUserId, out var userUnreads))
            {
                userUnreads.TryGetValue(group.GroupId, out unreadCount);
            }

            groupsData.Add(new Dictionary<string, object>
            {
                ["group_id"] = group.GroupId,
                ["group_name"] = group.Name,
                ["group_type"] = (int)group.Type,
                ["auto_group_prototype_id"] = group.AutoGroupPrototypeId ?? string.Empty,
                ["owner_id"] = group.OwnerId ?? string.Empty,
                ["members"] = membersList,
                ["unread_count"] = unreadCount
            });
        }

        var unreadCountsData = new Dictionary<string, int>();
        if (component.UnreadCounts.TryGetValue(senderUserId, out var senderUnreads))
        {
            foreach (var (chatId, count) in senderUnreads)
            {
                unreadCountsData[chatId] = count;
            }
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdGroupsList,
            ["groups"] = groupsData,
            ["unread_counts"] = unreadCountsData
        };

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        if (pdaFrequency.HasValue)
        {
            _deviceNetwork.SendPacket(uid, args.SenderAddress, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }
        else
        {
            _deviceNetwork.SendPacket(uid, args.SenderAddress, ref payload);
        }
    }

    private void HandleGetMessages(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        if (!args.Data.TryGetValue(MessengerCommands.CmdGetMessages, out NetworkPayload? messageRequest))
            return;

        if (!messageRequest.TryGetValue("chat_id", out string? chatId))
            return;

        var userId = args.SenderAddress;

        if (!string.IsNullOrEmpty(userId))
        {
            component.OpenChats[userId] = chatId;
        }

        if (!string.IsNullOrEmpty(userId) && component.UnreadCounts.TryGetValue(userId, out var userUnreads))
        {
            userUnreads.Remove(chatId);
        }

        if (!string.IsNullOrEmpty(userId) && component.MessageHistory.TryGetValue(chatId, out var chatMessages))
        {
            var updatedSenders = new HashSet<string>();
            var hasUpdates = false;
            foreach (var message in chatMessages)
            {
                if (message.RecipientId == userId && !string.IsNullOrEmpty(message.RecipientId) && !message.IsRead)
                {
                    message.IsRead = true;
                    hasUpdates = true;

                    if (!string.IsNullOrEmpty(message.SenderId) && message.SenderId != userId)
                    {
                        updatedSenders.Add(message.SenderId);
                    }
                }
            }

            if (updatedSenders.Count > 0)
            {
                uint? updatePdaFrequency = null;
                if (ProtoMan.TryIndex(component.PdaFrequencyId, out var updatePdaFreq))
                {
                    updatePdaFrequency = updatePdaFreq.Frequency;
                }

                foreach (var senderId in updatedSenders)
                {
                    var sortedChatMessages = chatMessages.OrderBy(m => m.MessageId).ToList();

                    var senderMessagesData = new List<Dictionary<string, object>>();
                    foreach (var msg in sortedChatMessages)
                    {
                        if (msg.SenderId == senderId || msg.RecipientId == senderId)
                        {
                            senderMessagesData.Add(new Dictionary<string, object>
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
                    }

                    var updatePayload = new NetworkPayload
                    {
                        [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessagesList,
                        ["messages"] = senderMessagesData,
                        ["chat_id"] = chatId
                    };

                    if (updatePdaFrequency.HasValue)
                    {
                        _deviceNetwork.SendPacket(uid, senderId, ref updatePayload, frequency: updatePdaFrequency, network: serverDevice.DeviceNetId);
                    }
                    else
                    {
                        _deviceNetwork.SendPacket(uid, senderId, ref updatePayload);
                    }
                }
            }

            if (hasUpdates)
            {
                var sortedChatMessages = chatMessages.OrderBy(m => m.MessageId).ToList();

                var recipientMessagesData = new List<Dictionary<string, object>>();
                foreach (var msg in sortedChatMessages)
                {
                    recipientMessagesData.Add(new Dictionary<string, object>
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

                var recipientPayload = new NetworkPayload
                {
                    [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessagesList,
                    ["messages"] = recipientMessagesData,
                    ["chat_id"] = chatId
                };

                uint? recipientPdaFrequency = null;
                if (ProtoMan.TryIndex(component.PdaFrequencyId, out var recipientPdaFreq))
                {
                    recipientPdaFrequency = recipientPdaFreq.Frequency;
                }

                if (recipientPdaFrequency.HasValue)
                {
                    _deviceNetwork.SendPacket(uid, userId, ref recipientPayload, frequency: recipientPdaFrequency, network: serverDevice.DeviceNetId);
                }
                else
                {
                    _deviceNetwork.SendPacket(uid, userId, ref recipientPayload);
                }
            }
        }

        if (!component.MessageHistory.TryGetValue(chatId, out var messages))
            messages = new List<MessengerMessage>();

        var sortedMessages = messages.OrderBy(m => m.MessageId).ToList();

        var messagesData = new List<Dictionary<string, object>>();
        foreach (var message in sortedMessages)
        {
            messagesData.Add(new Dictionary<string, object>
            {
                ["sender_id"] = message.SenderId,
                ["sender_name"] = message.SenderName,
                ["content"] = message.Content,
                ["timestamp"] = message.Timestamp.TotalSeconds,
                ["group_id"] = message.GroupId ?? string.Empty,
                ["recipient_id"] = message.RecipientId ?? string.Empty,
                ["is_read"] = message.IsRead,
                ["message_id"] = message.MessageId,
                ["sender_job_icon_id"] = message.SenderJobIconId?.Id ?? string.Empty,
                ["image_path"] = message.ImagePath ?? string.Empty
            });
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessagesList,
            ["messages"] = messagesData,
            ["chat_id"] = chatId
        };

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        if (pdaFrequency.HasValue)
        {
            _deviceNetwork.SendPacket(uid, args.SenderAddress, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }
        else
        {
            _deviceNetwork.SendPacket(uid, args.SenderAddress, ref payload);
        }
    }
}

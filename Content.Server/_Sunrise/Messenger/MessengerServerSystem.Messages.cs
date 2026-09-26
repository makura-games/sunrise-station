using System.Linq;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using DeviceNetworkPacketEvent = Content.Shared.DeviceNetwork.Events.DeviceNetworkPacketEvent<Content.Shared._Sunrise.DeviceNetwork.SunriseNetworkPayload>;
using Content.Shared._Sunrise.Messenger;

namespace Content.Server._Sunrise.Messenger;

/// <summary>
/// Часть системы мессенджера, отвечающая за обработку сообщений
/// </summary>
public sealed partial class MessengerServerSystem
{
    private void HandleSendMessage(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(MessengerCommands.CmdSendMessage, out NetworkPayload? messageData))
            return;

        if (!messageData.TryGetValue("content", out string? content))
            return;

        string? imagePath = null;
        if (_photoUploadEnabled && messageData.TryGetValue("image_path", out string? imgPath) && !string.IsNullOrWhiteSpace(imgPath))
        {
            imagePath = imgPath;
        }

        if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(imagePath))
            return;

        if (!component.Users.TryGetValue(args.SenderAddress, out var sender))
            return;

        var currentTime = _timing.CurTime;
        if (component.LastMessageTime.TryGetValue(sender.UserId, out var lastTime))
        {
            if (currentTime - lastTime < component.MessageCooldown)
            {
                Sawmill.Debug($"Message from {sender.UserId} rejected due to cooldown");
                return;
            }
        }

        UpdateUserInfoFromPda(uid, component, args.SenderAddress, sender);

        var timestamp = GetStationTime();
        component.LastMessageTime[sender.UserId] = currentTime;

        if (messageData.TryGetValue("group_id", out string? groupId) && !string.IsNullOrWhiteSpace(groupId))
        {
            SendGroupMessage(uid, component, sender, groupId, content, timestamp, imagePath);
        }
        else if (messageData.TryGetValue("recipient_id", out string? recipientId) && !string.IsNullOrWhiteSpace(recipientId))
        {
            SendPersonalMessage(uid, component, sender, recipientId, content, timestamp, imagePath);
        }
    }

    private void SendPersonalMessage(EntityUid uid, MessengerServerComponent component, MessengerUser sender, string recipientId, string content, TimeSpan timestamp, string? imagePath = null)
    {
        if (!component.Users.ContainsKey(recipientId))
            return;

        var messageId = GetNextMessageId(uid, component);
        var message = new MessengerMessage(sender.UserId, sender.Name, content, timestamp, null, recipientId, isRead: false, messageId, sender.JobIconId, imagePath);
        var chatId = GetPersonalChatId(sender.UserId, recipientId);

        if (!component.MessageHistory.TryGetValue(chatId, out var history))
        {
            history = new List<MessengerMessage>();
            component.MessageHistory[chatId] = history;
        }

        history.Add(message);
        TrimMessageHistory(history, component.MaxMessageHistory);

        var isChatOpen = component.OpenChats.TryGetValue(recipientId, out var openChatId) && openChatId == chatId;

        if (isChatOpen)
        {
            message.IsRead = true;
        }
        else
        {
            if (!component.UnreadCounts.TryGetValue(recipientId, out var recipientUnreads))
            {
                recipientUnreads = new Dictionary<string, int>();
                component.UnreadCounts[recipientId] = recipientUnreads;
            }
            recipientUnreads.TryGetValue(chatId, out var currentCount);
            recipientUnreads[chatId] = currentCount + 1;
        }

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessageReceived,
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
        };

        if (pdaFrequency.HasValue)
        {
            _deviceNetwork.SendPacket(uid, recipientId, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            _deviceNetwork.SendPacket(uid, sender.UserId, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }
        else
        {
            _deviceNetwork.SendPacket(uid, recipientId, ref payload);
            _deviceNetwork.SendPacket(uid, sender.UserId, ref payload);
        }

        if (isChatOpen && pdaFrequency.HasValue)
        {
            var updatePayload = new NetworkPayload
            {
                [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessagesList,
                ["messages"] = new List<Dictionary<string, object>>
                {
                    new ()
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
                    }
                },
                ["chat_id"] = chatId
            };
            _deviceNetwork.SendPacket(uid, sender.UserId, ref updatePayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }
    }

    private void SendGroupMessage(EntityUid uid, MessengerServerComponent component, MessengerUser sender, string groupId, string content, TimeSpan timestamp, string? imagePath = null)
    {
        if (!component.Groups.TryGetValue(groupId, out var group))
            return;

        if (!group.Members.Contains(sender.UserId))
            return;

        var messageId = GetNextMessageId(uid, component);
        var message = new MessengerMessage(sender.UserId, sender.Name, content, timestamp, groupId, null, false, messageId, sender.JobIconId, imagePath);

        if (!component.MessageHistory.TryGetValue(groupId, out var history))
        {
            history = new List<MessengerMessage>();
            component.MessageHistory[groupId] = history;
        }

        history.Add(message);
        TrimMessageHistory(history, component.MaxMessageHistory);

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        foreach (var memberId in group.Members)
        {
            if (memberId == sender.UserId)
                continue;

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
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessageReceived,
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
    }

    /// <summary>
    /// Отправляет личное сообщение с изображением (используется PhotoCartridgeSystem)
    /// </summary>
    public void SendPersonalMessageWithImage(EntityUid uid, string senderUserId, string recipientId, string content, string imagePath, TimeSpan timestamp)
    {
        if (!TryComp<MessengerServerComponent>(uid, out var component))
            return;

        if (!component.Users.TryGetValue(senderUserId, out var sender))
            return;

        SendPersonalMessage(uid, component, sender, recipientId, content, timestamp, _photoUploadEnabled ? imagePath : null);
    }

    /// <summary>
    /// Отправляет групповое сообщение с изображением (используется PhotoCartridgeSystem)
    /// </summary>
    public void SendGroupMessageWithImage(EntityUid uid, string senderUserId, string groupId, string content, string imagePath, TimeSpan timestamp)
    {
        if (!TryComp<MessengerServerComponent>(uid, out var component))
            return;

        if (!component.Users.TryGetValue(senderUserId, out var sender))
            return;

        SendGroupMessage(uid, component, sender, groupId, content, timestamp, _photoUploadEnabled ? imagePath : null);
    }

    /// <summary>
    /// Отправляет системное сообщение в группу (используется для оповещений департаментов)
    /// </summary>
    public void SendSystemMessageToGroup(EntityUid uid, string groupId, string content)
    {
        if (!TryComp<MessengerServerComponent>(uid, out var component))
            return;

        if (!component.Groups.TryGetValue(groupId, out var group))
            return;

        var timestamp = GetStationTime();
        var messageId = GetNextMessageId(uid, component);
        var senderName = Loc.GetString("messenger-system-name");
        var message = new MessengerMessage("system", senderName, content, timestamp, groupId, null, false, messageId);

        if (!component.MessageHistory.TryGetValue(groupId, out var history))
        {
            history = new List<MessengerMessage>();
            component.MessageHistory[groupId] = history;
        }

        history.Add(message);
        TrimMessageHistory(history, component.MaxMessageHistory);

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessageReceived,
            ["sender_id"] = message.SenderId,
            ["sender_name"] = message.SenderName,
            ["content"] = message.Content,
            ["timestamp"] = message.Timestamp.TotalSeconds,
            ["group_id"] = message.GroupId ?? string.Empty,
            ["recipient_id"] = message.RecipientId ?? string.Empty,
            ["is_read"] = message.IsRead,
            ["message_id"] = message.MessageId,
            ["sender_job_icon_id"] = string.Empty,
            ["image_path"] = string.Empty
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
                _deviceNetwork.SendPacket(uid, memberId, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, memberId, ref payload);
            }
        }
    }

    /// <summary>
    /// Обрабатывает удаление сообщения пользователем
    /// </summary>
    private void HandleDeleteMessage(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(MessengerCommands.CmdDeleteMessage, out NetworkPayload? deleteData))
            return;

        if (!deleteData.TryGetValue("message_id", out object? messageIdObj) ||
            !deleteData.TryGetValue("chat_id", out string? chatId))
            return;

        if (!long.TryParse(messageIdObj?.ToString(), out var messageId))
            return;

        if (!component.Users.TryGetValue(args.SenderAddress, out var user))
            return;

        if (!component.MessageHistory.TryGetValue(chatId, out var history))
            return;

        var message = history.FirstOrDefault(m => m.MessageId == messageId);
        if (message == null)
            return;

        if (message.SenderId != user.UserId)
            return;

        history.Remove(message);

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        var recipients = new HashSet<string>();

        if (chatId.StartsWith("personal_"))
        {
            var parts = chatId.Split('_');
            if (parts.Length >= 3)
            {
                recipients.Add(parts[1]);
                recipients.Add(parts[2]);
            }
        }
        else
        {
            if (component.Groups.TryGetValue(chatId, out var group))
            {
                foreach (var memberId in group.Members)
                {
                    recipients.Add(memberId);
                }
            }
        }

        var deletePayload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessageDeleted,
            ["message_id"] = messageId,
            ["chat_id"] = chatId
        };

        foreach (var recipientId in recipients)
        {
            if (pdaFrequency.HasValue)
            {
                _deviceNetwork.SendPacket(uid, recipientId, ref deletePayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
            else
            {
                _deviceNetwork.SendPacket(uid, recipientId, ref deletePayload);
            }
        }
    }
    /// <summary>
    /// Sends a fake message to a specific user (used for spam).
    /// </summary>
    public void SendFakePersonalMessage(EntityUid uid, string recipientId, string senderId, string senderName, string content)
    {
        if (!TryComp<MessengerServerComponent>(uid, out var component))
            return;

        if (!component.Users.ContainsKey(recipientId))
            return;

        var timestamp = GetStationTime();
        var messageId = GetNextMessageId(uid, component);
        var message = new MessengerMessage(senderId, senderName, content, timestamp, null, recipientId, isRead: false, messageId);
        var chatId = GetPersonalChatId(senderId, recipientId);

        if (!component.MessageHistory.TryGetValue(chatId, out var history))
        {
            history = new List<MessengerMessage>();
            component.MessageHistory[chatId] = history;
        }

        history.Add(message);
        TrimMessageHistory(history, component.MaxMessageHistory);

        var isChatOpen = component.OpenChats.TryGetValue(recipientId, out var openChatId) && openChatId == chatId;

        if (isChatOpen)
        {
            message.IsRead = true;
        }
        else
        {
            if (!component.UnreadCounts.TryGetValue(recipientId, out var recipientUnreads))
            {
                recipientUnreads = new Dictionary<string, int>();
                component.UnreadCounts[recipientId] = recipientUnreads;
            }
            recipientUnreads.TryGetValue(chatId, out var currentCount);
            recipientUnreads[chatId] = currentCount + 1;
        }

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
            return;

        uint? pdaFrequency = null;
        if (ProtoMan.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessageReceived,
            ["sender_id"] = message.SenderId,
            ["sender_name"] = message.SenderName,
            ["content"] = message.Content,
            ["timestamp"] = message.Timestamp.TotalSeconds,
            ["group_id"] = message.GroupId ?? string.Empty,
            ["recipient_id"] = message.RecipientId ?? string.Empty,
            ["is_read"] = message.IsRead,
            ["message_id"] = message.MessageId,
            ["sender_job_icon_id"] = string.Empty,
            ["image_path"] = string.Empty
        };

        if (pdaFrequency.HasValue)
        {
            _deviceNetwork.SendPacket(uid, recipientId, ref payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }
        else
        {
            _deviceNetwork.SendPacket(uid, recipientId, ref payload);
        }
    }
}

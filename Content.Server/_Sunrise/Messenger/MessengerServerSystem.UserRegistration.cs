using System.Linq;
using Content.Server._Sunrise.CartridgeLoader.Cartridges;
using Content.Server.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared._Sunrise.Messenger;
using Content.Shared.PDA;
using Content.Shared.Access.Components;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.GameTicking;
using Content.Shared.CartridgeLoader;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Messenger;

/// <summary>
/// Часть системы мессенджера, отвечающая за регистрацию пользователей
/// </summary>
public sealed partial class MessengerServerSystem
{
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        EntityUid? pdaUid = null;

        if (_inventory.TryGetSlotEntity(args.Mob, "idcard", out var idCardEntity) &&
            TryComp<PdaComponent>(idCardEntity, out _))
        {
            pdaUid = idCardEntity;
        }
        else if (_inventory.TryGetSlotEntity(args.Mob, "belt", out var beltEntity) &&
                 TryComp<PdaComponent>(beltEntity, out _))
        {
            pdaUid = beltEntity;
        }
        else
        {
            var handsQuery = EntityQueryEnumerator<PdaComponent>();
            while (handsQuery.MoveNext(out var uid, out var pda))
            {
                if (pda.PdaOwner == args.Mob)
                {
                    pdaUid = uid;
                    break;
                }
            }
        }

        if (pdaUid == null)
        {
            Sawmill.Warning($"No PDA found for player: {ToPrettyString(args.Mob)}");
            return;
        }

        if (!TryComp<PdaComponent>(pdaUid.Value, out var pdaComp) || pdaComp.PdaOwner == null)
        {
            Sawmill.Debug($"PDA {ToPrettyString(pdaUid.Value)} has no owner, skipping automatic registration");
            return;
        }

        var station = _stationSystem.GetOwningStation(args.Mob);
        if (station == null)
        {
            var mobXform = Transform(args.Mob);
            station = _stationSystem.GetStations().FirstOrDefault(s => Transform(s).MapID == mobXform.MapID);
        }

        if (station == null)
            station = _stationSystem.GetStations().FirstOrDefault();

        if (station == null)
        {
            Sawmill.Warning($"No station found for player: {ToPrettyString(args.Mob)}");
            return;
        }

        Sawmill.Debug($"Player station: {ToPrettyString(station.Value)}");

        var serverQuery = EntityQueryEnumerator<MessengerServerComponent, SingletonDeviceNetServerComponent, DeviceNetworkComponent>();
        EntityUid? serverUid = null;
        MessengerServerComponent? serverComponent = null;
        DeviceNetworkComponent? serverDevice = null;

        int serverCount = 0;
        while (serverQuery.MoveNext(out var uid, out var comp, out var singleton, out var device))
        {
            serverCount++;
            var serverStation = _stationSystem.GetOwningStation(uid);

            if (serverStation != station)
                continue;

            if (!_singletonServer.IsActiveServer(uid, singleton))
            {
                continue;
            }

            serverUid = uid;
            serverComponent = comp;
            serverDevice = device;
            break;
        }

        if (serverCount == 0)
        {
            Sawmill.Warning($"No messenger servers found on station: {ToPrettyString(station.Value)}");
        }

        if (serverUid == null || serverComponent == null || serverDevice == null)
        {
            Sawmill.Warning($"No active messenger server found for player: {ToPrettyString(args.Mob)}");
            return;
        }

        Sawmill.Debug($"Server DeviceNetwork before registration: Address={serverDevice.Address}, TransmitFrequency={serverDevice.TransmitFrequency}, ReceiveFrequency={serverDevice.ReceiveFrequency}");

        RegisterUserFromPda(serverUid.Value, serverComponent, pdaUid.Value);
    }

    /// <summary>
    /// Регистрирует пользователя на сервере мессенджера по его PDA
    /// </summary>
    private void RegisterUserFromPda(EntityUid uid, MessengerServerComponent component, EntityUid pdaUid)
    {
        if (!TryComp<PdaComponent>(pdaUid, out var pda))
        {
            Sawmill.Warning($"PDA component not found: {ToPrettyString(pdaUid)}");
            return;
        }

        if (!TryComp<DeviceNetworkComponent>(pdaUid, out var pdaDevice))
        {
            Sawmill.Warning($"DeviceNetwork component not found on PDA: {ToPrettyString(pdaUid)}");
            return;
        }

        if (string.IsNullOrEmpty(pdaDevice.Address))
        {
            return;
        }

        var userId = pdaDevice.Address;
        var userName = pda.OwnerName ?? Loc.GetString("messenger-user-unknown");

        string? jobTitle = null;
        var departments = new List<string>();
        ProtoId<JobIconPrototype> jobIconId = "JobIconUnknown";

        if (pda.ContainedId != null && TryComp<IdCardComponent>(pda.ContainedId.Value, out var idCard))
        {
            jobTitle = idCard.LocalizedJobTitle;
            departments.AddRange(idCard.JobDepartments.Select(d => (string) d));
            jobIconId = idCard.JobIcon;
        }

        var user = new MessengerUser(userId, userName, jobTitle, departments, jobIconId);
        component.Users[userId] = user;

        AddUserToAutoGroups(uid, component, userId, userName, departments);

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
        {
            Sawmill.Warning($"Server does not have DeviceNetworkComponent: {ToPrettyString(uid)}");
            return;
        }

        uint? pdaFrequency;
        if (_prototypeManager.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }
        else
        {
            Sawmill.Error($"PDA frequency prototype not found");
            return;
        }

        var response = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdUserRegistered,
            ["user_id"] = userId,
            ["user_name"] = userName,
            ["job_title"] = jobTitle ?? string.Empty,
            ["department_id"] = user.DepartmentId ?? string.Empty,
            ["department_ids"] = user.DepartmentIds,
            ["job_icon_id"] = jobIconId.Id
        };

        if (_deviceNetwork.IsAddressPresent(serverDevice.DeviceNetId, userId))
        {
            _deviceNetwork.QueuePacket(uid, userId, response, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }

        var usersPayload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdUsersList,
            ["users"] = component.Users.Values.Select(u => new Dictionary<string, object>
            {
                ["user_id"] = u.UserId,
                ["user_name"] = u.Name,
                ["job_title"] = u.JobTitle ?? string.Empty,
                ["department_id"] = u.DepartmentId ?? string.Empty,
                ["department_ids"] = u.DepartmentIds,
                ["job_icon_id"] = u.JobIconId?.Id ?? string.Empty
            }).ToList()
        };

        if (_deviceNetwork.IsAddressPresent(serverDevice.DeviceNetId, userId))
        {
            _deviceNetwork.QueuePacket(uid, userId, usersPayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }

        var groupsList = component.Groups.Values.ToList();
        var groupsData = new List<Dictionary<string, object>>();

        foreach (var group in groupsList)
        {
            if (group.GroupId == "common")
            {
                if (!group.Members.Contains(userId))
                    continue;
            }
            else if (group.GroupId.StartsWith("dept_"))
            {
                if (!group.Members.Contains(userId))
                    continue;
            }
            else if (group.GroupId.StartsWith("group_"))
            {
                if (!group.Members.Contains(userId))
                    continue;
            }

            var membersList = new List<object>();
            foreach (var memberId in group.Members)
            {
                membersList.Add(memberId);
            }

            var unreadCount = 0;
            if (component.UnreadCounts.TryGetValue(userId, out var userUnreads))
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
        if (component.UnreadCounts.TryGetValue(userId, out var senderUnreads))
        {
            foreach (var (chatId, count) in senderUnreads)
            {
                unreadCountsData[chatId] = count;
            }
        }

        var groupsPayload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdGroupsList,
            ["groups"] = groupsData,
            ["unread_counts"] = unreadCountsData
        };

        if (_deviceNetwork.IsAddressPresent(serverDevice.DeviceNetId, userId))
        {
            _deviceNetwork.QueuePacket(uid, userId, groupsPayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }

        if (_cartridgeLoader.TryGetProgram<MessengerCartridgeComponent>(pdaUid, out var cartridgeUid, out _))
        {
            if (TryComp<CartridgeLoaderComponent>(pdaUid, out var loader) &&
                !loader.BackgroundPrograms.Contains(cartridgeUid.Value))
            {
                _cartridgeLoader.RegisterBackgroundProgram(pdaUid, cartridgeUid.Value);
                Sawmill.Debug($"Registered messenger cartridge {ToPrettyString(cartridgeUid.Value)} as background program for PDA {ToPrettyString(pdaUid)}");
            }
        }
    }

    private void HandleRegisterUser(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(MessengerCommands.CmdRegisterUser, out NetworkPayload? userData))
        {
            return;
        }

        if (!userData.TryGetValue("pda_uid", out NetEntity netPdaUid))
        {
            return;
        }

        var pdaUid = GetEntity(netPdaUid);

        if (!TryComp<PdaComponent>(pdaUid, out var pda))
        {
            Sawmill.Warning($"PDA component not found: {ToPrettyString(pdaUid)}");
            return;
        }

        var userId = args.SenderAddress;

        string? userName = null;
        string? jobTitle = null;
        var departments = new List<string>();
        ProtoId<JobIconPrototype> jobIconId = "JobIconUnknown";

        if (pda.ContainedId != null && TryComp<IdCardComponent>(pda.ContainedId.Value, out var idCard))
        {
            userName = idCard.FullName;
            jobTitle = idCard.LocalizedJobTitle;
            departments.AddRange(idCard.JobDepartments.Select(d => (string) d));
            jobIconId = idCard.JobIcon;
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = pda.OwnerName ?? Loc.GetString("messenger-user-unknown");
        }

        var user = new MessengerUser(userId, userName, jobTitle, departments, jobIconId);
        component.Users[userId] = user;

        AddUserToAutoGroups(uid, component, userId, userName, departments);

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
        {
            Sawmill.Warning($"Server does not have DeviceNetworkComponent: {ToPrettyString(uid)}");
            return;
        }

        uint? pdaFrequency;
        if (_prototypeManager.TryIndex(component.PdaFrequencyId, out var pdaFreq))
        {
            pdaFrequency = pdaFreq.Frequency;
        }
        else
        {
            Sawmill.Error($"PDA frequency prototype not found");
            return;
        }

        var response = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdUserRegistered,
            ["user_id"] = userId,
            ["user_name"] = userName,
            ["job_title"] = jobTitle ?? string.Empty,
            ["department_id"] = user.DepartmentId ?? string.Empty,
            ["department_ids"] = user.DepartmentIds,
            ["job_icon_id"] = jobIconId.Id
        };

        if (_deviceNetwork.IsAddressPresent(serverDevice.DeviceNetId, args.SenderAddress))
        {
            _deviceNetwork.QueuePacket(uid, args.SenderAddress, response, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
        }

        var notifyPayload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdUsersList,
            ["users"] = component.Users.Values.Select(u => new Dictionary<string, object>
            {
                ["user_id"] = u.UserId,
                ["user_name"] = u.Name,
                ["job_title"] = u.JobTitle ?? string.Empty,
                ["department_id"] = u.DepartmentId ?? string.Empty,
                ["department_ids"] = u.DepartmentIds,
                ["job_icon_id"] = u.JobIconId?.Id ?? string.Empty
            }).ToList()
        };

        foreach (var existingUserId in component.Users.Keys)
        {
            if (existingUserId == userId)
                continue;

            if (_deviceNetwork.IsAddressPresent(serverDevice.DeviceNetId, existingUserId))
            {
                _deviceNetwork.QueuePacket(uid, existingUserId, notifyPayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
            }
        }

        if (_cartridgeLoader.TryGetProgram<MessengerCartridgeComponent>(pdaUid, out var cartridgeUid, out _))
        {
            if (TryComp<CartridgeLoaderComponent>(pdaUid, out var loader) &&
                !loader.BackgroundPrograms.Contains(cartridgeUid.Value))
            {
                _cartridgeLoader.RegisterBackgroundProgram(pdaUid, cartridgeUid.Value);
                Sawmill.Debug($"Registered messenger cartridge {ToPrettyString(cartridgeUid.Value)} as background program for PDA {ToPrettyString(pdaUid)}");
            }
        }
    }

    /// <summary>
    /// Создает автоматические группы на основе прототипов
    /// </summary>
    private void CreateAutoGroups(MessengerServerComponent component)
    {
        int created = 0;
        foreach (var autoGroupProto in _prototypeManager.EnumeratePrototypes<MessengerAutoGroupPrototype>())
        {
            if (component.Groups.ContainsKey(autoGroupProto.GroupId))
                continue;

            var group = new MessengerGroup(
                autoGroupProto.GroupId,
                _loc.GetString(autoGroupProto.Name),
                new HashSet<string>(),
                MessengerGroupType.Automatic,
                autoGroupProto.ID
            );
            component.Groups[autoGroupProto.GroupId] = group;
            created++;
        }

        Sawmill.Info($"Created {created} automatic messenger groups");
    }

    /// <summary>
    /// Добавляет пользователя в автоматические группы на основе прототипов
    /// </summary>
    private void AddUserToAutoGroups(EntityUid uid, MessengerServerComponent component, string userId, string userName, IEnumerable<string> departments)
    {
        foreach (var autoGroupProto in _prototypeManager.EnumeratePrototypes<MessengerAutoGroupPrototype>())
        {
            bool shouldAdd = false;

            if (autoGroupProto.AddAllUsers)
            {
                shouldAdd = true;
            }
            else if (autoGroupProto.Departments.Count > 0)
            {
                shouldAdd = autoGroupProto.Departments.Any(d => departments.Contains((string) d));
            }

            if (!shouldAdd)
                continue;

            if (!component.Groups.TryGetValue(autoGroupProto.GroupId, out var group))
            {
                group = new MessengerGroup(
                    autoGroupProto.GroupId,
                    _loc.GetString(autoGroupProto.Name),
                    new HashSet<string>(),
                    MessengerGroupType.Automatic,
                    autoGroupProto.ID
                );
                component.Groups[autoGroupProto.GroupId] = group;
            }

            if (!group.Members.Contains(userId))
            {
                group.Members.Add(userId);

                if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
                    continue;

                uint? pdaFrequency = null;
                if (_prototypeManager.TryIndex(component.PdaFrequencyId, out var pdaFreq))
                {
                    pdaFrequency = pdaFreq.Frequency;
                }

                var isDepartmentChat = group.Type == MessengerGroupType.Automatic && group.AutoGroupPrototypeId != null;

                if (!isDepartmentChat)
                {
                    var timestamp = GetStationTime();
                    var messageText = Loc.GetString("messenger-system-user-added", ("userName", userName));
                    var messageId = GetNextMessageId(uid, component);
                    var systemMessage = new MessengerMessage("system", Loc.GetString("messenger-system-name"), messageText, timestamp, autoGroupProto.GroupId, null, false, messageId);

                    if (!component.MessageHistory.TryGetValue(autoGroupProto.GroupId, out var history))
                    {
                        history = new List<MessengerMessage>();
                        component.MessageHistory[autoGroupProto.GroupId] = history;
                    }
                    history.Add(systemMessage);
                    TrimMessageHistory(history, component.MaxMessageHistory);
                }

                if (!isDepartmentChat)
                {
                    var timestamp = GetStationTime();
                    var messageText = Loc.GetString("messenger-system-user-added", ("userName", userName));
                    var messageId = GetNextMessageId(uid, component);
                    var systemMessage = new MessengerMessage("system", Loc.GetString("messenger-system-name"), messageText, timestamp, autoGroupProto.GroupId, null, false, messageId);

                    var messagePayload = new NetworkPayload
                    {
                        [DeviceNetworkConstants.Command] = MessengerCommands.CmdMessageReceived,
                        ["sender_id"] = "system",
                        ["sender_name"] = Loc.GetString("messenger-system-name"),
                        ["content"] = messageText,
                        ["timestamp"] = timestamp.TotalSeconds,
                        ["group_id"] = autoGroupProto.GroupId,
                        ["recipient_id"] = string.Empty,
                        ["is_read"] = false,
                        ["message_id"] = systemMessage.MessageId,
                        ["image_path"] = systemMessage.ImagePath ?? string.Empty
                    };

                    foreach (var memberId in group.Members)
                    {
                        var isMemberChatOpen = component.OpenChats.TryGetValue(memberId, out var memberOpenChatId) && memberOpenChatId == autoGroupProto.GroupId;

                        if (!isMemberChatOpen)
                        {
                            if (!component.UnreadCounts.TryGetValue(memberId, out var memberUnreads))
                            {
                                memberUnreads = new Dictionary<string, int>();
                                component.UnreadCounts[memberId] = memberUnreads;
                            }
                            memberUnreads.TryGetValue(autoGroupProto.GroupId, out var currentCount);
                            memberUnreads[autoGroupProto.GroupId] = currentCount + 1;
                        }

                        if (pdaFrequency.HasValue)
                        {
                            _deviceNetwork.QueuePacket(uid, memberId, messagePayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
                        }
                        else
                        {
                            _deviceNetwork.QueuePacket(uid, memberId, messagePayload);
                        }
                    }
                }

                if (component.MessageHistory.TryGetValue(autoGroupProto.GroupId, out var groupHistory) && groupHistory.Count > 0)
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
                        ["chat_id"] = autoGroupProto.GroupId
                    };

                    if (pdaFrequency.HasValue)
                    {
                        _deviceNetwork.QueuePacket(uid, userId, historyPayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
                    }
                    else
                    {
                        _deviceNetwork.QueuePacket(uid, userId, historyPayload);
                    }
                }

                var payload = new NetworkPayload
                {
                    [DeviceNetworkConstants.Command] = MessengerCommands.CmdUserAddedToGroup,
                    ["group_id"] = autoGroupProto.GroupId,
                    ["user_id"] = userId
                };

                foreach (var memberId in group.Members)
                {
                    if (memberId == userId)
                        continue;

                    if (pdaFrequency.HasValue)
                    {
                        _deviceNetwork.QueuePacket(uid, memberId, payload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
                    }
                    else
                    {
                        _deviceNetwork.QueuePacket(uid, memberId, payload);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Обновляет информацию о пользователе из его PDA по адресу DeviceNetwork
    /// </summary>
    private void UpdateUserInfoFromPda(EntityUid uid, MessengerServerComponent component, string userId, MessengerUser user)
    {
        var pdaQuery = EntityQueryEnumerator<PdaComponent, DeviceNetworkComponent>();
        EntityUid? foundPda = null;

        while (pdaQuery.MoveNext(out var pdaUid, out var pda, out var deviceNetwork))
        {
            if (deviceNetwork.Address == userId)
            {
                foundPda = pdaUid;
                break;
            }
        }

        if (foundPda == null)
            return;

        if (!TryComp<PdaComponent>(foundPda.Value, out var pdaComp))
            return;

        string? jobTitle = null;
        var departments = new List<string>();
        ProtoId<JobIconPrototype> jobIconId = "JobIconUnknown";

        if (pdaComp.ContainedId != null && TryComp<IdCardComponent>(pdaComp.ContainedId.Value, out var idCard))
        {
            jobTitle = idCard.LocalizedJobTitle;
            departments.AddRange(idCard.JobDepartments.Select(d => (string) d));
            jobIconId = idCard.JobIcon;
        }

        var needsUpdate = user.JobTitle != jobTitle || !user.DepartmentIds.SequenceEqual(departments) || user.JobIconId != jobIconId;

        if (needsUpdate)
        {
            user.JobTitle = jobTitle;
            user.DepartmentIds = departments;
            user.JobIconId = jobIconId;

            if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
                return;

            uint? pdaFrequency = null;
            if (_prototypeManager.TryIndex(component.PdaFrequencyId, out var pdaFreq))
            {
                pdaFrequency = pdaFreq.Frequency;
            }

            var notifyPayload = new NetworkPayload
            {
                [DeviceNetworkConstants.Command] = MessengerCommands.CmdUsersList,
                ["users"] = component.Users.Values.Select(u => new Dictionary<string, object>
                {
                    ["user_id"] = u.UserId,
                    ["user_name"] = u.Name,
                    ["job_title"] = u.JobTitle ?? string.Empty,
                    ["department_id"] = u.DepartmentId ?? string.Empty,
                    ["department_ids"] = u.DepartmentIds,
                    ["job_icon_id"] = u.JobIconId?.Id ?? string.Empty
                }).ToList()
            };

            foreach (var existingUserId in component.Users.Keys)
            {
                if (_deviceNetwork.IsAddressPresent(serverDevice.DeviceNetId, existingUserId))
                {
                    if (pdaFrequency.HasValue)
                    {
                        _deviceNetwork.QueuePacket(uid, existingUserId, notifyPayload, frequency: pdaFrequency, network: serverDevice.DeviceNetId);
                    }
                    else
                    {
                        _deviceNetwork.QueuePacket(uid, existingUserId, notifyPayload);
                    }
                }
            }
        }
    }
}

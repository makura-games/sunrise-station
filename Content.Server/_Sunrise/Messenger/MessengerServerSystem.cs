using System.Linq;
using Content.Shared.Radio;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.DeviceNetwork.Events;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared._Sunrise.Messenger;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Content.Server.CartridgeLoader;
using Content.Server.DeviceNetwork.Components;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Random;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Messenger;

/// <summary>
/// Система сервера мессенджера, обрабатывающая сообщения между КПК
/// </summary>
public sealed partial class MessengerServerSystem : EntitySystem
{
    [Dependency] private DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private SingletonDeviceNetServerSystem _singletonServer = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    private ISawmill Sawmill { get; set; } = default!;
    private bool _photoUploadEnabled = true;

    /// <summary>
    /// Находит GroupId для указанного радиоканала
    /// </summary>
    public string? GetGroupIdByRadioChannel(string radioChannelId)
    {
        foreach (var proto in _prototypeManager.EnumeratePrototypes<MessengerAutoGroupPrototype>())
        {
            if (proto.RadioChannel == radioChannelId)
                return proto.GroupId;
        }

        return null;
    }

    public override void Initialize()
    {
        base.Initialize();

        Sawmill = _logManager.GetSawmill("messenger.server");

        _cfg.OnValueChanged(SunriseCCVars.PhotoUploadEnabled, value => _photoUploadEnabled = value, true);

        SubscribeLocalEvent<MessengerServerComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        SubscribeLocalEvent<MessengerServerComponent, DeviceNetServerConnectedEvent>(OnServerConnected);
        SubscribeLocalEvent<MessengerServerComponent, DeviceNetServerDisconnectedEvent>(OnServerDisconnected);
        SubscribeLocalEvent<MessengerServerComponent, RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        InitializeSpam();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateSpam(frameTime);
    }

    private void OnRoundRestart(EntityUid uid, MessengerServerComponent component, RoundRestartCleanupEvent args)
    {
        component.Users.Clear();
        component.Groups.Clear();
        component.MessageHistory.Clear();
        component.LastMessageTime.Clear();
        component.GroupIdCounter = 0;
        component.MessageIdCounter = 0;
    }

    private void OnServerConnected(EntityUid uid, MessengerServerComponent component, ref DeviceNetServerConnectedEvent args)
    {
        Sawmill.Info($"Messenger server connected: {ToPrettyString(uid)}");

        if (!TryComp<DeviceNetworkComponent>(uid, out var serverDevice))
        {
            Sawmill.Error($"Server DeviceNetwork component not found: {ToPrettyString(uid)}");
            return;
        }

        if (!_deviceNetwork.IsDeviceConnected(uid, serverDevice))
        {
            if (!_deviceNetwork.ConnectDevice(uid, serverDevice))
            {
                return;
            }
        }

        CreateAutoGroups(component);

        foreach (var user in component.Users.Values)
        {
            AddUserToAutoGroups(uid, component, user.UserId, user.Name, user.DepartmentIds);
        }
    }

    private void OnServerDisconnected(EntityUid uid, MessengerServerComponent component, ref DeviceNetServerDisconnectedEvent args)
    {
    }

    private void OnPacketReceived(EntityUid uid, MessengerServerComponent component, DeviceNetworkPacketEvent args)
    {
        if (!_singletonServer.IsActiveServer(uid))
        {
            return;
        }

        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out string? command))
        {
            return;
        }

        switch (command)
        {
            case MessengerCommands.CmdRegisterUser:
                HandleRegisterUser(uid, component, args);
                break;
            case MessengerCommands.CmdSendMessage:
                HandleSendMessage(uid, component, args);
                break;
            case MessengerCommands.CmdCreateGroup:
                HandleCreateGroup(uid, component, args);
                break;
            case MessengerCommands.CmdAddToGroup:
                HandleAddToGroup(uid, component, args);
                break;
            case MessengerCommands.CmdRemoveFromGroup:
                HandleRemoveFromGroup(uid, component, args);
                break;
            case MessengerCommands.CmdGetUsers:
                HandleGetUsers(uid, component, args);
                break;
            case MessengerCommands.CmdGetGroups:
                HandleGetGroups(uid, component, args);
                break;
            case MessengerCommands.CmdGetMessages:
                HandleGetMessages(uid, component, args);
                break;
            case MessengerCommands.CmdAcceptInvite:
                HandleAcceptInvite(uid, component, args);
                break;
            case MessengerCommands.CmdDeclineInvite:
                HandleDeclineInvite(uid, component, args);
                break;
            case MessengerCommands.CmdLeaveGroup:
                HandleLeaveGroup(uid, component, args);
                break;
            case MessengerCommands.CmdDeleteMessage:
                HandleDeleteMessage(uid, component, args);
                break;
            default:
                Sawmill.Warning($"Unknown command received: {command} from {args.SenderAddress}");
                break;
        }
    }

    /// <summary>
    /// Генерирует ID для личного чата между двумя пользователями
    /// </summary>
    private string GetPersonalChatId(string userId1, string userId2)
    {
        var ids = new[] { userId1, userId2 }.OrderBy(x => x).ToArray();
        return $"personal_{ids[0]}_{ids[1]}";
    }

    /// <summary>
    /// Ограничивает историю сообщений до указанного количества
    /// </summary>
    private void TrimMessageHistory(List<MessengerMessage> history, int maxCount)
    {
        if (history.Count > maxCount)
        {
            var toRemove = history.Count - maxCount;
            history.RemoveRange(0, toRemove);
        }
    }

    /// <summary>
    /// Получает время станции (обычное время, как в КПК)
    /// </summary>
    public TimeSpan GetStationTime()
    {
        return (DateTime.UtcNow + TimeSpan.FromHours(3)) - new DateTime(2024, 1, 1);
    }

    /// <summary>
    /// Получает следующий уникальный ID сообщения
    /// </summary>
    private long GetNextMessageId(EntityUid uid, MessengerServerComponent component)
    {
        return ++component.MessageIdCounter;
    }

    /// <summary>
    /// Находит сущность сервера мессенджера для станции
    /// </summary>
    public (EntityUid, MessengerServerComponent)? GetServerEntity(EntityUid? station)
    {
        if (station == null)
            return null;

        var query = EntityQueryEnumerator<MessengerServerComponent, SingletonDeviceNetServerComponent>();
        while (query.MoveNext(out var uid, out var comp, out var singleton))
        {
            if (!_singletonServer.IsActiveServer(uid, singleton))
                continue;

            if (_stationSystem.GetOwningStation(uid) != station)
                continue;

            return (uid, comp);
        }

        return null;
    }
}

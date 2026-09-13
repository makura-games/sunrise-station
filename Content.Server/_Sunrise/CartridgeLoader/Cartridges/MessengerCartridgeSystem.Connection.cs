using Content.Server._Sunrise.Messenger;
using Content.Server.DeviceNetwork.Components;
using Content.Shared._Sunrise.Messenger;
using Content.Shared.CartridgeLoader;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.PDA;

namespace Content.Server._Sunrise.CartridgeLoader.Cartridges;

/// <summary>
/// Часть системы картриджа мессенджера, отвечающая за подключение к серверу
/// </summary>
public sealed partial class MessengerCartridgeSystem
{
    private void CheckServerStatus(EntityUid uid, MessengerCartridgeComponent component, EntityUid loaderUid, bool updateUi = true)
    {
        if (!TryGetPdaAndDeviceNetwork(loaderUid, out var pdaUid, out _))
        {
            if (updateUi)
                UpdateUiState(uid, loaderUid, component);
            return;
        }

        if (!TryGetMessengerServerAddress(pdaUid, out var serverAddress))
        {
            component.ServerAddress = null;
            component.IsRegistered = false;
            component.UserId = null;
            component.LastRegistrationAttempt = null;
            component.Users.Clear();
            component.Groups.Clear();
            component.MessageHistory.Clear();
            component.ServerUnreadCounts.Clear();
            if (updateUi)
                UpdateUiState(uid, loaderUid, component);
            return;
        }

        if (component.ServerAddress != serverAddress)
        {
            var wasNull = component.ServerAddress == null;
            var wasDifferent = !wasNull && component.ServerAddress != serverAddress;

            component.ServerAddress = serverAddress;
            component.IsRegistered = false;
            component.UserId = null;
            component.LastRegistrationAttempt = null;

            if (wasDifferent)
            {
                component.Users.Clear();
                component.Groups.Clear();
                component.MessageHistory.Clear();
                component.ServerUnreadCounts.Clear();
            }

            if (wasNull && TryGetPdaAndDeviceNetwork(loaderUid, out _, out var deviceNetwork))
            {
                component.LastUsersUpdate = null;
                RequestUsers(uid, component, loaderUid, deviceNetwork);
                RequestGroups(uid, component, loaderUid, deviceNetwork);
            }

            if (TryGetPdaAndDeviceNetwork(loaderUid, out _, out _))
            {
                TryConnectToServer(uid, component, loaderUid);
            }
            if (updateUi)
                UpdateUiState(uid, loaderUid, component);
        }
        else if (!component.IsRegistered)
        {
            if (TryGetPdaAndDeviceNetwork(loaderUid, out _, out var deviceNetwork))
            {
                TryConnectToServer(uid, component, loaderUid);
            }
            if (updateUi)
                UpdateUiState(uid, loaderUid, component);
        }
        else
        {
            if (updateUi)
                UpdateUiState(uid, loaderUid, component);
        }
    }

    private void OnCartridgeActivated(EntityUid uid, MessengerCartridgeComponent component, CartridgeActivatedEvent args)
    {
        component.UiReady = false;

        if (component.LoaderUid == null)
        {
            component.LoaderUid = args.Loader;
        }

        CheckServerStatus(uid, component, args.Loader, updateUi: true);

        if (!component.IsRegistered && component.ServerAddress != null)
        {
            if (TryGetPdaAndDeviceNetwork(args.Loader, out var pdaUid, out var deviceNetwork))
            {
                RegisterUser(uid, component, args.Loader, deviceNetwork, pdaUid, allowWithoutOwner: true);
            }
        }
    }

    private void OnCartridgeDeactivated(EntityUid uid, MessengerCartridgeComponent component, CartridgeDeactivatedEvent args)
    {
        component.UiReady = false;
    }

    private void OnCartridgeAdded(EntityUid uid, MessengerCartridgeComponent component, CartridgeAddedEvent args)
    {
        component.LoaderUid = args.Loader;
        component.LastStatusCheck = null;
        component.UiReady = false;

        if (!TryGetPdaAndDeviceNetwork(args.Loader, out var pdaUid, out _))
        {
            return;
        }

        if (!TryComp<PdaComponent>(pdaUid, out var pda) || pda.PdaOwner == null)
        {
            Sawmill.Debug($"PDA {ToPrettyString(pdaUid)} has no owner, skipping background program registration");
            return;
        }

        TryConnectToServer(uid, component, args.Loader);

        _cartridgeLoader.RegisterBackgroundProgram(args.Loader, uid);

        CheckServerStatus(uid, component, args.Loader);
    }

    private void OnUiReady(EntityUid uid, MessengerCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        if (component.LoaderUid == null)
        {
            component.LoaderUid = args.Loader;
        }

        component.UiReady = true;
        RaiseMessengerOpened(args.Loader, args.Actor);

        if (component.IsRegistered && component.ServerAddress != null)
        {
            if (TryGetPdaAndDeviceNetwork(args.Loader, out _, out var deviceNetwork))
            {
                if (component.Users.Count == 0 || component.Groups.Count == 0)
                {
                    RequestUsers(uid, component, args.Loader, deviceNetwork);
                    RequestGroups(uid, component, args.Loader, deviceNetwork);
                }
            }
        }

        UpdateUiState(uid, args.Loader, component);
    }

    private void OnLoaderUiClosed(Entity<CartridgeLoaderComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!ent.Comp.UiKey.Equals(args.UiKey) ||
            ent.Comp.ActiveProgram is not { } activeProgram ||
            !TryComp<MessengerCartridgeComponent>(activeProgram, out var messenger))
        {
            return;
        }

        messenger.UiReady = false;
    }

    private void RaiseMessengerOpened(EntityUid pdaUid, EntityUid actor = default)
    {
        if (actor.IsValid() && Exists(actor))
        {
            RaiseLocalEvent(actor, new MessengerOpenedEvent(actor, pdaUid));
            return;
        }

        if (!TryComp<PdaComponent>(pdaUid, out var pda) ||
            pda.PdaOwner is not { } owner)
        {
            return;
        }

        RaiseLocalEvent(owner, new MessengerOpenedEvent(owner, pdaUid));
    }

    private void TryConnectToServer(EntityUid uid, MessengerCartridgeComponent component, EntityUid loaderUid)
    {
        if (!TryGetPdaAndDeviceNetwork(loaderUid, out var pdaUid, out var deviceNetwork))
        {
            Sawmill.Warning($"Failed to get PDA and DeviceNetwork: {ToPrettyString(loaderUid)}");
            return;
        }

        component.LoaderUid = loaderUid;

        if (!TryGetMessengerServerAddress(pdaUid, out var serverAddress))
        {
            Sawmill.Warning($"No active messenger server found for PDA: {ToPrettyString(pdaUid)}");
            component.ServerAddress = null;
            component.IsRegistered = false;
            UpdateUiState(uid, loaderUid, component);
            return;
        }

        if (string.IsNullOrEmpty(serverAddress))
        {
            Sawmill.Warning($"Server address is empty, server may not be connected to DeviceNetwork yet");
            component.ServerAddress = null;
            component.IsRegistered = false;
            UpdateUiState(uid, loaderUid, component);
            return;
        }

        Sawmill.Debug($"Found active server address: {serverAddress}");

        if (component.ServerAddress != serverAddress)
        {
            component.ServerAddress = serverAddress;
            component.IsRegistered = false;
            component.UserId = null;
            component.LastRegistrationAttempt = null;
        }

        if (component.IsRegistered && component.ServerAddress == serverAddress)
        {
            Sawmill.Debug($"Already registered, updating UI state");
            UpdateUiState(uid, loaderUid, component);
            return;
        }

        if (!component.IsRegistered)
        {
            if (TryComp<PdaComponent>(pdaUid, out var pda) && pda.PdaOwner != null)
            {
                RegisterUser(uid, component, loaderUid, deviceNetwork, pdaUid);
            }
            else
            {
                Sawmill.Debug($"PDA {ToPrettyString(pdaUid)} has no owner, skipping automatic registration");
                UpdateUiState(uid, loaderUid, component);
            }
        }
    }

    private bool TryGetMessengerServerAddress(EntityUid pdaUid, out string? serverAddress)
    {
        var station = _stationSystem.GetOwningStation(pdaUid);
        if (station != null &&
            _singletonServer.TryGetActiveServerAddress<MessengerServerComponent>(station.Value, out serverAddress))
        {
            return true;
        }

        if (TryGetSameMapMessengerServerAddress(pdaUid, out serverAddress))
            return true;

        station = GetBestStation(pdaUid);
        return station != null &&
               _singletonServer.TryGetActiveServerAddress<MessengerServerComponent>(station.Value, out serverAddress);
    }

    private bool TryGetSameMapMessengerServerAddress(EntityUid pdaUid, out string? serverAddress)
    {
        serverAddress = null;

        if (!TryComp(pdaUid, out TransformComponent? pdaTransform))
            return false;

        var query = EntityQueryEnumerator<
            MessengerServerComponent,
            SingletonDeviceNetServerComponent,
            DeviceNetworkComponent,
            TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var singleton, out var device, out var transform))
        {
            if (transform.MapID != pdaTransform.MapID ||
                !_singletonServer.IsActiveServer(uid, singleton))
            {
                continue;
            }

            if (string.IsNullOrEmpty(device.Address) &&
                !_deviceNetwork.IsDeviceConnected(uid, device))
            {
                _deviceNetwork.ConnectDevice(uid, device);
            }

            if (string.IsNullOrEmpty(device.Address))
                continue;

            serverAddress = device.Address;
            return true;
        }

        return false;
    }

    private void RegisterUser(EntityUid uid, MessengerCartridgeComponent component, EntityUid loaderUid, DeviceNetworkComponent deviceNetwork, EntityUid pdaUid, bool allowWithoutOwner = false)
    {
        if (!allowWithoutOwner)
        {
            if (!TryComp<PdaComponent>(pdaUid, out var pda) || pda.PdaOwner == null)
            {
                Sawmill.Debug($"PDA {ToPrettyString(pdaUid)} has no owner, skipping automatic registration");
                return;
            }
        }

        if (component.ServerAddress == null)
        {
            Sawmill.Warning($"Cannot register: ServerAddress is null");
            return;
        }

        var currentTime = _gameTiming.CurTime;
        if (component.LastRegistrationAttempt.HasValue)
        {
            var timeSinceLastAttempt = currentTime - component.LastRegistrationAttempt.Value;
            if (timeSinceLastAttempt.TotalSeconds < 5.0)
            {
                Sawmill.Debug($"Registration attempt too soon, waiting: {timeSinceLastAttempt.TotalSeconds:F2}s");
                return;
            }
        }

        component.LastRegistrationAttempt = currentTime;

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = MessengerCommands.CmdRegisterUser,
            [MessengerCommands.CmdRegisterUser] = new NetworkPayload
            {
                ["pda_uid"] = GetNetEntity(pdaUid)
            }
        };

        uint? messengerFrequency = GetMessengerFrequency();
        if (!messengerFrequency.HasValue)
        {
            return;
        }

        if (!_deviceNetwork.IsAddressPresent(deviceNetwork.DeviceNetId, component.ServerAddress))
        {
            Sawmill.Warning($"Server address {component.ServerAddress} is not present in network {deviceNetwork.DeviceNetId}");
            return;
        }

        SetMessengerFrequency(loaderUid, deviceNetwork, out var originalFreq);

        if (!TryComp<DeviceNetworkComponent>(loaderUid, out var pdaDevice))
        {
            Sawmill.Error($"Failed to get DeviceNetworkComponent after setting frequency");
            RestoreFrequency(loaderUid, deviceNetwork, originalFreq);
            return;
        }

        Sawmill.Debug($"PDA DeviceNetwork: Address={pdaDevice.Address}, TransmitFrequency={pdaDevice.TransmitFrequency}, ReceiveFrequency={pdaDevice.ReceiveFrequency}, DeviceNetId={pdaDevice.DeviceNetId}");

        if (!_deviceNetwork.IsAddressPresent(pdaDevice.DeviceNetId, component.ServerAddress))
        {
            Sawmill.Warning($"Server address {component.ServerAddress} is not present in network {pdaDevice.DeviceNetId} (PDA network)");
        }
        else
        {
            Sawmill.Debug($"Server address {component.ServerAddress} found in network {pdaDevice.DeviceNetId}");
        }

        var pdaTransform = Transform(loaderUid);
        var pdaPos = _transformSystem.GetWorldPosition(pdaTransform);
        Sawmill.Debug($"PDA position: {pdaPos}, MapId: {pdaTransform.MapID}");

        _deviceNetwork.QueuePacket(loaderUid, component.ServerAddress, payload, frequency: messengerFrequency, network: pdaDevice.DeviceNetId);

        RestoreFrequency(loaderUid, pdaDevice, originalFreq);
    }
}

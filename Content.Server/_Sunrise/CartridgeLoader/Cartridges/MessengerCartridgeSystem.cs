using System.Linq;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.CartridgeLoader;
using Content.Server.PDA.Ringer;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.CartridgeLoader;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;

namespace Content.Server._Sunrise.CartridgeLoader.Cartridges;

/// <summary>
/// Система картриджа мессенджера для КПК
/// </summary>
public sealed partial class MessengerCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private SingletonDeviceNetServerSystem _singletonServer = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private RingerSystem _ringer = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private ISawmill Sawmill { get; set; } = default!;
    private const string MessengerFrequencyId = "Messenger";
    private bool _photoUploadEnabled = true;

    public override void Initialize()
    {
        base.Initialize();

        Sawmill = _logManager.GetSawmill("messenger.cartridge");

        _cfg.OnValueChanged(SunriseCCVars.PhotoUploadEnabled, value => _photoUploadEnabled = value, true);

        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeActivatedEvent>(OnCartridgeActivated);
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeDeactivatedEvent>(OnCartridgeDeactivated);
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeDeviceNetPacketEvent>(OnPacketReceived);
        SubscribeLocalEvent<CartridgeLoaderComponent, BoundUIClosedEvent>(OnLoaderUiClosed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MessengerCartridgeComponent>();
        var currentTime = _gameTiming.CurTime;

        while (query.MoveNext(out var uid, out var component))
        {
            if (component.LoaderUid == null)
                continue;

            if (!TryComp<CartridgeLoaderComponent>(component.LoaderUid.Value, out var loader))
                continue;

            var isActive = loader.ActiveProgram == uid;
            var isBackground = loader.BackgroundPrograms.Contains(uid);

            if (!isActive && !isBackground)
                continue;

            if (component.LastStatusCheck.HasValue)
            {
                var timeSinceLastCheck = currentTime - component.LastStatusCheck.Value;
                if (timeSinceLastCheck.TotalSeconds < 2.0)
                    continue;
            }

            component.LastStatusCheck = currentTime;

            CheckServerStatus(uid, component, component.LoaderUid.Value);
        }
    }

    private bool TryGetPdaAndDeviceNetwork(EntityUid loaderUid, out EntityUid pdaUid, out DeviceNetworkComponent deviceNetwork)
    {
        pdaUid = EntityUid.Invalid;
        deviceNetwork = null!;

        if (!TryComp<DeviceNetworkComponent>(loaderUid, out var device))
            return false;

        pdaUid = loaderUid;
        deviceNetwork = device;
        return true;
    }

    /// <summary>
    /// Получает частоту Messenger
    /// </summary>
    private uint? GetMessengerFrequency()
    {
        if (_prototypeManager.TryIndex<DeviceFrequencyPrototype>(MessengerFrequencyId, out var messengerFrequency))
        {
            return messengerFrequency.Frequency;
        }
        Sawmill.Error($"Messenger frequency prototype not found: {MessengerFrequencyId}");
        return null;
    }

    /// <summary>
    /// Устанавливает частоту передачи на Messenger
    /// </summary>
    private void SetMessengerFrequency(EntityUid loaderUid, DeviceNetworkComponent deviceNetwork, out uint? originalFrequency)
    {
        originalFrequency = deviceNetwork.TransmitFrequency;
        var messengerFreq = GetMessengerFrequency();
        if (messengerFreq.HasValue)
        {
            _deviceNetwork.SetTransmitFrequency(loaderUid, messengerFreq.Value, deviceNetwork);
        }
    }

    /// <summary>
    /// Восстанавливает исходную частоту передачи
    /// </summary>
    private void RestoreFrequency(EntityUid loaderUid, DeviceNetworkComponent deviceNetwork, uint? originalFrequency)
    {
        if (originalFrequency.HasValue)
        {
            _deviceNetwork.SetTransmitFrequency(loaderUid, originalFrequency.Value, deviceNetwork);
        }
    }

    /// <summary>
    /// Пытается найти станцию для КПК. Если КПК не на станции, ищет любую станцию на той же карте.
    /// </summary>
    private EntityUid? GetBestStation(EntityUid pdaUid)
    {
        var station = _stationSystem.GetOwningStation(pdaUid);
        if (station != null)
            return station;

        var xform = Transform(pdaUid);
        var mapId = xform.MapID;

        foreach (var s in _stationSystem.GetStations())
        {
            if (Transform(s).MapID == mapId)
                return s;
        }

        return _stationSystem.GetStations().FirstOrDefault();
    }
}

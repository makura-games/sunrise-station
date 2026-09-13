using System.Linq;
using Content.Server.CartridgeLoader;
using Content.Server.DeviceNetwork.Systems;
using Content.Server._Sunrise.Messenger;
using Content.Server.Station.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared._Sunrise.CartridgeLoader.Cartridges;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Server._Sunrise.CartridgeLoader.Cartridges;

public sealed partial class PhotoCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private NetTexturesManager _netTexturesManager = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private MessengerServerSystem _messengerServer = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private SingletonDeviceNetServerSystem _singletonServer = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private ISawmill _sawmill = default!;

    private const int MaxPhotosPerUser = 50;
    private const int MaxPhotoSizeBytes = 2 * 1024 * 1024;
    private const double MinTimeBetweenCapturesSeconds = 1.0;
    private const int MaxPhotoWidth = 512;
    private const int MaxPhotoHeight = 512;

    private bool _photoUploadEnabled = true;
    private bool _photoCaptureEnabled = true;

    private readonly Dictionary<ICommonSession, TimeSpan> _lastCaptureTimes = new();

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("photo.cartridge");

        _cfg.OnValueChanged(SunriseCCVars.PhotoUploadEnabled, value => _photoUploadEnabled = value, true);
        _cfg.OnValueChanged(SunriseCCVars.PhotoCaptureEnabled, value => _photoCaptureEnabled = value, true);

        SubscribeLocalEvent<PhotoCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<PhotoCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);

        _netTexturesManager.OnPhotoCaptureMessage = OnPhotoCaptureMessage;
    }

    private void OnUiMessage(EntityUid uid, PhotoCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not PhotoUiMessageEvent photoMessage)
            return;

        var loaderUid = GetEntity(args.LoaderUid);
        if (loaderUid == EntityUid.Invalid)
            return;

        switch (photoMessage.Action)
        {
            case PhotoUiAction.CapturePhoto:
                HandleCapturePhoto(uid, component, loaderUid);
                break;
            case PhotoUiAction.SendPhotoToMessenger:
                HandleSendPhotoToMessenger(uid, component, loaderUid, photoMessage.PhotoId, photoMessage.RecipientId, photoMessage.GroupId);
                break;
            case PhotoUiAction.RequestGallery:
                UpdateUiState(uid, loaderUid, component);
                break;
            case PhotoUiAction.DeletePhoto:
                HandleDeletePhoto(uid, component, loaderUid, photoMessage.PhotoId);
                break;
            case PhotoUiAction.ToggleFlash:
                if (photoMessage.FlashEnabled.HasValue)
                {
                    component.FlashEnabled = photoMessage.FlashEnabled.Value;
                    UpdateUiState(uid, loaderUid, component);
                }
                break;
        }
    }

    private void HandleDeletePhoto(EntityUid uid, PhotoCartridgeComponent component, EntityUid loaderUid, string? photoId)
    {
        if (string.IsNullOrEmpty(photoId) || !component.PhotoGallery.TryGetValue(photoId, out var metadata))
            return;

        component.PhotoGallery.Remove(photoId);

        UpdateUiState(uid, loaderUid, component);
    }

    private void OnUiReady(EntityUid uid, PhotoCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }

    private void HandleCapturePhoto(EntityUid uid, PhotoCartridgeComponent component, EntityUid loaderUid)
    {
        if (!_photoCaptureEnabled)
        {
            UpdateUiState(uid, loaderUid, component, errorMessage: Loc.GetString("photo-cartridge-capture-disabled"));
            return;
        }

        if (component.PhotoGallery.Count >= MaxPhotosPerUser)
        {
            UpdateUiState(uid, loaderUid, component, errorMessage: Loc.GetString("photo-cartridge-limit-reached"));
            return;
        }

        if (component.FlashEnabled)
        {
            Spawn(component.FlashEffect, _transform.GetMapCoordinates(loaderUid));
        }

        _audio.PlayPvs(component.Sound, loaderUid);

        UpdateUiState(uid, loaderUid, component);
    }

    /// <summary>
    /// Обработчик сетевого сообщения с захваченным изображением от клиента
    /// </summary>
    private void OnPhotoCaptureMessage(PdaPhotoCaptureMessage msg)
    {
        if (!_photoCaptureEnabled)
            return;

        if (!_playerManager.TryGetSessionByChannel(msg.MsgChannel, out var session))
            return;

        if (_lastCaptureTimes.TryGetValue(session, out var lastCapture))
        {
            var timeSinceLastCapture = _messengerServer.GetStationTime() - lastCapture;
            if (timeSinceLastCapture.TotalSeconds < MinTimeBetweenCapturesSeconds)
            {
                _sawmill.Debug($"Photo capture rejected: cooldown active ({timeSinceLastCapture.TotalSeconds:F2}s < {MinTimeBetweenCapturesSeconds}s)");
                return;
            }
        }

        if (msg.ImageData.Length > MaxPhotoSizeBytes)
        {
            _sawmill.Warning($"Photo capture rejected: image too large ({msg.ImageData.Length} bytes > {MaxPhotoSizeBytes} bytes)");
            return;
        }

        if (msg.Width <= 0 || msg.Height <= 0 || msg.Width > MaxPhotoWidth || msg.Height > MaxPhotoHeight)
        {
            _sawmill.Warning($"Photo capture rejected: invalid dimensions ({msg.Width}x{msg.Height})");
            return;
        }

        var entity = GetEntity(msg.LoaderUid);
        PhotoCartridgeComponent? photoComponent;
        EntityUid? cartridgeUid;
        EntityUid pdaUid = EntityUid.Invalid;

        if (_cartridgeLoader.TryGetProgram(entity, out cartridgeUid, out photoComponent))
        {
            pdaUid = entity;
        }
        else if (TryComp(entity, out photoComponent) && TryComp<CartridgeComponent>(entity, out var cartridge) && cartridge.LoaderUid.HasValue)
        {
            pdaUid = cartridge.LoaderUid.Value;
            cartridgeUid = entity;
        }

        if (photoComponent == null || cartridgeUid == null)
        {
            _sawmill.Warning($"Photo capture rejected: photo cartridge not found for entity {ToPrettyString(entity)}");
            return;
        }

        if (photoComponent.PhotoGallery.Count >= MaxPhotosPerUser)
        {
            UpdateUiState(cartridgeUid!.Value, pdaUid, photoComponent, errorMessage: Loc.GetString("photo-cartridge-limit-reached"));
            return;
        }

        var photoId = Guid.NewGuid().ToString();
        var imagePath = $"/NetTextures/Messenger/{photoId}.png";

        _netTexturesManager.RegisterDynamicResource(imagePath, msg.ImageData);

        var timestamp = _messengerServer.GetStationTime();
        var metadata = new PhotoMetadata(photoId, imagePath, timestamp);
        photoComponent.PhotoGallery[photoId] = metadata;

        _lastCaptureTimes[session] = timestamp;

        _sawmill.Info($"Photo captured from {session.Name}: {photoId}, path: {imagePath}, size: {msg.Width}x{msg.Height}, {msg.ImageData.Length} bytes");

        UpdateUiState(cartridgeUid!.Value, pdaUid, photoComponent);
    }

    private void HandleSendPhotoToMessenger(EntityUid uid, PhotoCartridgeComponent component, EntityUid loaderUid, string? photoId, string? recipientId, string? groupId)
    {
        if (!_photoUploadEnabled)
        {
            UpdateUiState(uid, loaderUid, component, errorMessage: Loc.GetString("photo-cartridge-upload-disabled"));
            return;
        }

        if (string.IsNullOrWhiteSpace(photoId))
            return;

        if (!component.PhotoGallery.TryGetValue(photoId, out var photoMetadata))
        {
            UpdateUiState(uid, loaderUid, component, errorMessage: Loc.GetString("photo-cartridge-photo-not-found"));
            return;
        }

        var messengerServer = FindMessengerServer(loaderUid);
        if (messengerServer == null)
        {
            UpdateUiState(uid, loaderUid, component, errorMessage: Loc.GetString("photo-cartridge-messenger-unavailable"));
            return;
        }

        if (!TryComp<DeviceNetworkComponent>(loaderUid, out var pdaDevice))
            return;

        var userId = pdaDevice.Address;
        if (string.IsNullOrEmpty(userId))
            return;

        var timestamp = _messengerServer.GetStationTime();
        var content = Loc.GetString("photo-cartridge-photo-text");

        if (!string.IsNullOrWhiteSpace(groupId))
        {
            _messengerServer.SendGroupMessageWithImage(messengerServer.Value, userId, groupId, content, photoMetadata.ImagePath, timestamp);
        }
        else if (!string.IsNullOrWhiteSpace(recipientId))
        {
            _messengerServer.SendPersonalMessageWithImage(messengerServer.Value, userId, recipientId, content, photoMetadata.ImagePath, timestamp);
        }
        else
        {
            UpdateUiState(uid, loaderUid, component, errorMessage: Loc.GetString("photo-cartridge-recipient-not-specified"));
            return;
        }

        UpdateUiState(uid, loaderUid, component);
    }

    private EntityUid? FindMessengerServer(EntityUid pdaUid)
    {
        var station = _stationSystem.GetOwningStation(pdaUid);
        if (station == null)
        {
            var xform = Transform(pdaUid);
            var mapId = xform.MapID;
            foreach (var s in _stationSystem.GetStations())
            {
                if (Transform(s).MapID == mapId)
                {
                    station = s;
                    break;
                }
            }
        }

        if (station == null)
            station = _stationSystem.GetStations().FirstOrDefault();

        if (station == null)
            return null;

        if (!_singletonServer.TryGetActiveServerAddress<MessengerServerComponent>(station.Value, out var serverAddress))
            return null;

        var serverQuery = EntityQueryEnumerator<MessengerServerComponent, DeviceNetworkComponent>();
        while (serverQuery.MoveNext(out var uid, out _, out var deviceNetwork))
        {
            if (deviceNetwork.Address == serverAddress && _singletonServer.IsActiveServer(uid))
            {
                return uid;
            }
        }
        return null;
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, PhotoCartridgeComponent component, string? errorMessage = null)
    {
        var state = new PhotoUiState(
            photos: component.PhotoGallery,
            cameraReady: _photoCaptureEnabled && component.PhotoGallery.Count < MaxPhotosPerUser,
            photoSendingEnabled: _photoUploadEnabled,
            flashEnabled: component.FlashEnabled,
            errorMessage: errorMessage
        );
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

}

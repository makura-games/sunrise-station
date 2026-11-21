using System.IO;
using Content.Shared.CartridgeLoader;
using System.Linq;
using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.Mobs.Components;
using Robust.Client.Graphics;
using Robust.Client.State;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.CartridgeLoader.Cartridges;

public sealed partial class PhotoCartridgeClientSystem : EntitySystem
{
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private NetTexturesManager _netTexturesManager = default!;

    private TimeSpan _nextCaptureTime = TimeSpan.Zero;
    public bool CameraReady => _timing.CurTime >= _nextCaptureTime;
    public float CaptureDistance { get; set; } = 2.0f;
    public float MinCaptureDistance { get; set; } = 1.0f;

    private ISawmill _sawmill = default!;

    private const int TargetPhotoWidth = 256;
    private const int TargetPhotoHeight = 256;

    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("photo.cartridge.client");
    }

    public override void Shutdown()
    {
    }

    public Vector2 GetCameraPosition(EntityUid source, float distance)
    {
        if (!_entityManager.TryGetComponent<TransformComponent>(source, out var xform))
            return Vector2.Zero;

        var sourcePos = _transformSystem.GetWorldPosition(source);

        var lookRotation = _transformSystem.GetWorldRotation(source);
        var ignoreEnt = source;

        if (xform.ParentUid.IsValid() && !_entityManager.HasComponent<MapComponent>(xform.ParentUid) && !_entityManager.HasComponent<MapGridComponent>(xform.ParentUid))
        {
            lookRotation = _transformSystem.GetWorldRotation(xform.ParentUid);
            ignoreEnt = xform.ParentUid;
        }

        var gridRot = Angle.Zero;
        if (xform.GridUid != null)
            gridRot = _transformSystem.GetWorldRotation(xform.GridUid.Value);

        var relativeRot = lookRotation - gridRot;
        var directionVec = gridRot.RotateVec(relativeRot.GetCardinalDir().ToVec());

        var ray = new CollisionRay(sourcePos, directionVec, (int) Content.Shared.Physics.CollisionGroup.Opaque);

        var results = _physics.IntersectRayWithPredicate(xform.MapID, ray, distance, uid =>
        {
            return uid == source ||
                   uid == ignoreEnt ||
                   _entityManager.HasComponent<MobStateComponent>(uid);
        }, false).ToList();

        if (results.Count > 0)
        {
            return sourcePos + directionVec * Math.Max(0.1f, results[0].Distance - 0.2f);
        }

        return sourcePos + directionVec * distance;
    }

    public void CaptureAndSendPhoto(EntityUid source)
    {
        if (TryComp<CartridgeComponent>(source, out var cart) && cart.LoaderUid.HasValue)
            source = cart.LoaderUid.Value;

        if (_timing.CurTime < _nextCaptureTime)
            return;

        _nextCaptureTime = _timing.CurTime + TimeSpan.FromSeconds(1.5);

        Timer.Spawn(500, () => CaptureInternal(source));
    }

    public Vector2 GetClampedCapturePosition(EntityUid source, float distance)
    {
        var targetPos = GetCameraPosition(source, distance);

        if (_stateManager.CurrentState is not IMainViewportState viewportState)
            return targetPos;

        var targetScreen = _eyeManager.WorldToScreen(targetPos);
        var sourcePos = _transformSystem.GetWorldPosition(source);
        var sourceScreen = _eyeManager.WorldToScreen(sourcePos);
        var offsetScreen = _eyeManager.WorldToScreen(sourcePos + new Vector2(1, 0));
        var pixelsPerMeter = (offsetScreen - sourceScreen).Length();

        var size = 3.0f * pixelsPerMeter;
        var control = (Control)viewportState.Viewport;
        var vpRect = control.GlobalPixelRect;

        size = Math.Min(size, Math.Min(vpRect.Width, vpRect.Height));
        var halfSize = size / 2f;

        var clampedX = Math.Clamp(targetScreen.X, vpRect.Left + halfSize, vpRect.Right - halfSize);
        var clampedY = Math.Clamp(targetScreen.Y, vpRect.Top + halfSize, vpRect.Bottom - halfSize);

        return _eyeManager.ScreenToMap(new Vector2(clampedX, clampedY)).Position;
    }

    private void CaptureInternal(EntityUid source)
    {
        if (!_netManager.IsConnected)
        {
            _sawmill.Warning("Cannot capture photo: client not connected to server");
            return;
        }

        if (_playerManager.LocalEntity is not { })
            return;

        if (_stateManager.CurrentState is not IMainViewportState viewportState)
            return;

        var viewport = viewportState.Viewport.Viewport;
        var sourcePos = _transformSystem.GetWorldPosition(source);
        var targetPos = GetClampedCapturePosition(source, CaptureDistance);

        var center = viewport.WorldToRenderTargetPixels(targetPos);
        var p1 = viewport.WorldToRenderTargetPixels(sourcePos);
        var p2 = viewport.WorldToRenderTargetPixels(sourcePos + new Vector2(1, 0));
        var pixelSize = 3.0f * (p2 - p1).Length();

        viewport.Screenshot(img => {
            var size = (int)pixelSize;
            size = Math.Min(size, Math.Min(img.Width, img.Height));

            var left = (int)(center.X - size / 2f);
            var top = (int)(center.Y - size / 2f);

            left = Math.Clamp(left, 0, img.Width - size);
            top = Math.Clamp(top, 0, img.Height - size);

            if (size <= 0)
            {
                img.Dispose();
                return;
            }

            var finalRect = new Rectangle(left, top, size, size);
            TakeScreenshot(source, img, finalRect);
        });
    }

    private void TakeScreenshot<T>(EntityUid source, Image<T> screenshot, Rectangle cropRect) where T : unmanaged, IPixel<T>
    {
        try
        {
            ProcessCapturedImage(source, screenshot, cropRect);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Error processing screenshot: {ex.Message}");
        }
        finally
        {
            screenshot.Dispose();
        }
    }

    private void ProcessCapturedImage<T>(EntityUid source, Image<T> image, Rectangle cropRect) where T : unmanaged, IPixel<T>
    {
        if (cropRect.Width <= 0 || cropRect.Height <= 0)
            return;

        var rescaled = new Image<T>(TargetPhotoWidth, TargetPhotoHeight);
        var rescaledSpan = rescaled.GetPixelSpan();
        var sourceSpan = image.GetPixelSpan();

        float scaleX = (float)cropRect.Width / TargetPhotoWidth;
        float scaleY = (float)cropRect.Height / TargetPhotoHeight;

        for (int y = 0; y < TargetPhotoHeight; y++)
        {
            for (int x = 0; x < TargetPhotoWidth; x++)
            {
                int srcX = cropRect.X + (int)(x * scaleX);
                int srcY = cropRect.Y + (int)(y * scaleY);

                if (srcX >= 0 && srcX < image.Width && srcY >= 0 && srcY < image.Height)
                {
                    rescaledSpan[y * TargetPhotoWidth + x] = sourceSpan[srcY * image.Width + srcX];
                }
            }
        }

        var width = rescaled.Width;
        var height = rescaled.Height;

        byte[] imageData;
        using (var memoryStream = new MemoryStream())
        {
            rescaled.SaveAsPng(memoryStream);
            imageData = memoryStream.ToArray();
        }

        rescaled.Dispose();
        SendPhotoToServer(source, imageData, width, height);
    }

    private void SendPhotoToServer(EntityUid source, byte[] imageData, int width, int height)
    {
        _netTexturesManager.SendPhotoToServer(_entityManager.GetNetEntity(source), imageData, width, height);
    }
}

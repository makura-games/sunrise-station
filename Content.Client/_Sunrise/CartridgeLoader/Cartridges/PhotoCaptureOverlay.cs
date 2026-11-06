using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Enums;
using Content.Client.Viewport;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.CartridgeLoader.Cartridges;

public sealed partial class PhotoCaptureOverlay : Overlay
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly SharedTransformSystem _transformSystem;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public PhotoCaptureOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transformSystem = _entityManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_stateManager.CurrentState is not IMainViewportState viewportState)
            return;

        if (args.Viewport.Eye != _eyeManager.CurrentEye)
            return;

        var screenHandle = args.ScreenHandle;
        var viewportControl = (Control)viewportState.Viewport;

        var overlaySystem = _entityManager.System<PhotoOverlaySystem>();
        var photoSystem = _entityManager.System<PhotoCartridgeClientSystem>();

        var source = overlaySystem.ActiveCameraEntity;

        if (source == null || !_entityManager.EntityExists(source.Value))
        {
             if (_playerManager.LocalSession?.AttachedEntity is not { } player)
                return;
             source = player;
        }

        var sourcePos = _transformSystem.GetWorldPosition(source.Value);
        var targetPos = photoSystem.GetClampedCapturePosition(source.Value, photoSystem.CaptureDistance);

        var targetScreen = _eyeManager.WorldToScreen(targetPos);
        var playerScreen = _eyeManager.WorldToScreen(sourcePos);
        var offsetScreen = _eyeManager.WorldToScreen(sourcePos + new Vector2(1, 0));
        var pixelsPerMeter = (offsetScreen - playerScreen).Length();

        if (pixelsPerMeter < 1) return;

        var vpRect = viewportControl.GlobalPixelRect;
        var size = 3.0f * pixelsPerMeter;

        size = Math.Min(size, Math.Min(vpRect.Width, vpRect.Height));
        var halfSize = size / 2.0f;

        var rect = UIBox2.FromDimensions(targetScreen.X - halfSize, targetScreen.Y - halfSize, size, size);
        var color = Color.Black.WithAlpha(0.5f);

        screenHandle.DrawRect(new UIBox2(vpRect.Left, vpRect.Top, vpRect.Right, rect.Top), color);
        screenHandle.DrawRect(new UIBox2(vpRect.Left, rect.Bottom, vpRect.Right, vpRect.Bottom), color);
        screenHandle.DrawRect(new UIBox2(vpRect.Left, rect.Top, rect.Left, rect.Bottom), color);
        screenHandle.DrawRect(new UIBox2(rect.Right, rect.Top, vpRect.Right, rect.Bottom), color);

        screenHandle.DrawRect(rect, Color.Red.WithAlpha(0.3f), false);
        var borderThickness = 2f;
        screenHandle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + borderThickness), Color.Red);
        screenHandle.DrawRect(new UIBox2(rect.Left, rect.Bottom - borderThickness, rect.Right, rect.Bottom), Color.Red);
        screenHandle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Left + borderThickness, rect.Bottom), Color.Red);
        screenHandle.DrawRect(new UIBox2(rect.Right - borderThickness, rect.Top, rect.Right, rect.Bottom), Color.Red);
    }
}

using Robust.Client.Graphics;

namespace Content.Client._Sunrise.CartridgeLoader.Cartridges;

public sealed partial class PhotoOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private PhotoCaptureOverlay? _overlay;
    public bool OverlayEnabled { get; private set; }
    public EntityUid? ActiveCameraEntity { get; set; }

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new PhotoCaptureOverlay();
    }

    public void SetOverlayEnabled(bool enabled, EntityUid? source = null)
    {
        OverlayEnabled = enabled;
        ActiveCameraEntity = source;

        if (enabled)
        {
            if (!_overlayManager.HasOverlay<PhotoCaptureOverlay>())
                _overlayManager.AddOverlay(_overlay!);
        }
        else
        {
            _overlayManager.RemoveOverlay<PhotoCaptureOverlay>();
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay<PhotoCaptureOverlay>();
    }
}

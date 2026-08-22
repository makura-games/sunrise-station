using Content.Shared._Sunrise.Mood;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._Sunrise.Overlays.Systems;

public sealed partial class SaturationScaleSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private ISharedPlayerManager _playerMan = default!;

    private SaturationScaleOverlay _overlay = default!;


    public override void Initialize()
    {
        base.Initialize();

        _overlay = new();

        SubscribeLocalEvent<SaturationScaleOverlayComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SaturationScaleOverlayComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<SaturationScaleOverlayComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SaturationScaleOverlayComponent, PlayerDetachedEvent>(OnPlayerDetached);

        SubscribeNetworkEvent<RoundRestartCleanupEvent>(RoundRestartCleanup);
    }


    private void RoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, SaturationScaleOverlayComponent component, PlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(EntityUid uid, SaturationScaleOverlayComponent component, PlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnShutdown(EntityUid uid, SaturationScaleOverlayComponent component, ComponentShutdown args)
    {
        if (uid != _playerMan.LocalEntity)
            return;

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnInit(EntityUid uid, SaturationScaleOverlayComponent component, ComponentInit args)
    {
        if (uid != _playerMan.LocalEntity)
            return;

        _overlayMan.AddOverlay(_overlay);
    }
}

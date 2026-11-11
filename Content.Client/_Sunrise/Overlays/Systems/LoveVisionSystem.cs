using Content.Shared._Sunrise.Aphrodisiac;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Sunrise.LoveVision;
public sealed partial class LoveVisionSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;

    private LoveVisionOverlay _overlay = default!;

    public static string LoveVisionKey = "LoveEffect";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LoveVisionComponent, ComponentInit>(OnLoveVisionInit);
        SubscribeLocalEvent<LoveVisionComponent, ComponentShutdown>(OnLoveVisionShutdown);

        SubscribeLocalEvent<LoveVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LoveVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new();
    }

    private void OnPlayerAttached(EntityUid uid, LoveVisionComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, LoveVisionComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnLoveVisionInit(EntityUid uid, LoveVisionComponent component, ComponentInit args)
    {
        if (_player.LocalSession?.AttachedEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnLoveVisionShutdown(EntityUid uid, LoveVisionComponent component, ComponentShutdown args)
    {
        if (_player.LocalSession?.AttachedEntity == uid)
        {
            _overlayMan.RemoveOverlay(_overlay);
        }
    }
}

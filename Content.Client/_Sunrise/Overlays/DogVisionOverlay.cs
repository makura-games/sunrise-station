// Inspired by Nyanotrasen

using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Content.Shared.Abilities;
using System.Numerics;

namespace Content.Client._Sunrise.Overlays;

public sealed partial class DogVisionOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] IEntityManager _entityManager = default!;
    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _dogVisionShader;
    private readonly string _dogVision = "DogVision";
    public DogVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _dogVisionShader = _prototypeManager.Index<ShaderPrototype>(_dogVision).Instance().Duplicate();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        if (_playerManager.LocalSession?.AttachedEntity is not { Valid: true } player)
            return;

        if (!_entityManager.HasComponent<DogVisionComponent>(player))
            return;

        _dogVisionShader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_dogVisionShader);
        worldHandle.DrawRect(viewport, Color.White);
    }
}

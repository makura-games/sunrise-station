using Content.Shared._Sunrise.ThermalVision;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.ThermalVision;

public sealed partial class ThermalVisionOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private static readonly ProtoId<ShaderPrototype> ScreenShader = "ThermalVisionScreenShader";

    private readonly TransformSystem _transform;
    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _screenShader;
    public ThermalVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _screenShader = _prototypeManager.Index(ScreenShader).InstanceUnique();
        _transform = _entityManager.System<TransformSystem>();
        ZIndex = 10000;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalSession?.AttachedEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        var playerEntity = _playerManager.LocalSession?.AttachedEntity;

        if (playerEntity == null)
            return false;

        if (!_entityManager.TryGetComponent<ThermalVisionComponent>(playerEntity, out var blurComp))
            return false;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;

        _screenShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        worldHandle.UseShader(_screenShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}

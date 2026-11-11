using Content.Shared._Sunrise.ThermalVision;
using Content.Shared.Body;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.ThermalVision;

public sealed partial class ThroughWallsVisionOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private static readonly ProtoId<ShaderPrototype> BrightnessShader = "BrightnessShader";

    private readonly ContainerSystem _containerSystem;
    private readonly TransformSystem _transform;
    private readonly ShaderInstance _shader;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public ThroughWallsVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entityManager.System<TransformSystem>();
        _containerSystem = _entityManager.System<ContainerSystem>();

        _shader = _prototypeManager.Index(BrightnessShader).InstanceUnique();
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

        worldHandle.UseShader(_shader);
        var query = _entityManager.EntityQueryEnumerator<BodyComponent, MetaDataComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var meta, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId || _containerSystem.IsEntityInContainer(uid, meta)) continue;
            var (position, rotation) = _transform.GetWorldPositionRotation(xform);

            sprite.Render(worldHandle, eyeRotation, rotation, null, position);
        }

        worldHandle.UseShader(null);
    }
}

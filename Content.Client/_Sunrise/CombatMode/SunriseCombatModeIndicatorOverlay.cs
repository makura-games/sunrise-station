using System.Numerics;
using Content.Client.CombatMode;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.CombatMode;

public sealed class SunriseCombatModeIndicatorOverlay : Overlay
{
    private static readonly ResPath IndicatorRsi = new("/Textures/_Sunrise/Interface/CombatMode/combat_mode.rsi");
    private static readonly RSI.StateId IndicatorState = new("icon");
    private static readonly UIBox2 IndicatorRegion =
        UIBox2.FromDimensions(new Vector2(1f, 1f), new Vector2(5f, 5f));
    private const float HeadGap = 2f / EyeManager.PixelsPerMeter;
    private const float IndicatorSize = 6f / EyeManager.PixelsPerMeter;
    private static readonly Vector2 IndicatorOffset =
        new(-11f / EyeManager.PixelsPerMeter, -14f / EyeManager.PixelsPerMeter);

    private readonly IEyeManager _eye;
    private readonly IPlayerManager _player;
    private readonly CombatModeSystem _combatMode;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly EntityQuery<SpriteComponent> _spriteQuery;
    private readonly EntityQuery<TransformComponent> _transformQuery;
    private readonly Texture[] _frames;
    private readonly float[] _frameDelays;

    private float _frameTime;
    private int _frameIndex;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public SunriseCombatModeIndicatorOverlay(
        IEntityManager entity,
        IEyeManager eye,
        IPlayerManager player,
        CombatModeSystem combatMode,
        IResourceCache resources)
    {
        _eye = eye;
        _player = player;
        _combatMode = combatMode;
        _sprite = entity.System<SpriteSystem>();
        _transform = entity.System<TransformSystem>();
        _spriteQuery = entity.GetEntityQuery<SpriteComponent>();
        _transformQuery = entity.GetEntityQuery<TransformComponent>();

        var rsi = resources.GetResource<RSIResource>(IndicatorRsi).RSI;
        if (!rsi.TryGetState(IndicatorState, out var state))
            throw new InvalidOperationException($"RSI {IndicatorRsi} has no state {IndicatorState}.");

        _frames = state.GetFrames(RsiDirection.South);
        _frameDelays = state.GetDelays();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { Valid: true } &&
               _combatMode.IsInCombatMode() &&
               args.Viewport.Eye == _eye.CurrentEye &&
               base.BeforeDraw(in args);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_frames.Length <= 1)
            return;

        _frameTime += args.DeltaSeconds;
        while (_frameTime >= _frameDelays[_frameIndex])
        {
            _frameTime -= _frameDelays[_frameIndex];
            _frameIndex = (_frameIndex + 1) % _frames.Length;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { Valid: true } player ||
            !_transformQuery.TryComp(player, out var transform))
            return;

        Entity<TransformComponent> playerTransform = (player, transform);
        if (playerTransform.Comp.MapID != args.MapId)
            return;

        var spriteTop = 0.5f;
        if (_spriteQuery.TryComp(playerTransform, out var sprite))
            spriteTop = _sprite.GetLocalBounds((playerTransform, sprite)).Top;

        var eyeRotation = args.Viewport.Eye?.Rotation ?? default;
        var headOffset = (-eyeRotation).ToWorldVec() *
                         -(spriteTop + IndicatorSize / 2f + HeadGap);
        var worldPosition = _transform.GetWorldPosition(playerTransform.Comp) + headOffset;

        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRotation);
        var positionMatrix = Matrix3Helpers.CreateTranslation(worldPosition);
        args.WorldHandle.SetTransform(Matrix3x2.Multiply(rotationMatrix, positionMatrix));

        var frame = _frames[_frameIndex];
        var size = Vector2.One * IndicatorSize;
        args.WorldHandle.DrawTextureRectRegion(
            frame,
            Box2.CenteredAround(IndicatorOffset, size),
            subRegion: IndicatorRegion);
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }
}

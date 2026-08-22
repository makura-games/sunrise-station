using System.Numerics;
using Content.Shared._RMC14.Explosion.Components;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Explosion;

public sealed partial class RMCExplosionShockWaveOverlay : Overlay, IEntityEventSubscriber
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private SharedTransformSystem? _xformSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;
    private readonly string _shockWave = "RMCShockWave";

    /// <summary>
    ///     Maximum number of distortions that can be shown on screen at a time.
    /// </summary>
    public const int MaxCount = 10;

    public RMCExplosionShockWaveOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index<ShaderPrototype>(_shockWave).Instance().Duplicate();
    }

    private readonly Vector2[] _positions = new Vector2[MaxCount];
    private readonly float[] _falloffPower = new float[MaxCount];
    private readonly float[] _sharpness = new float[MaxCount];
    private readonly float[] _width = new float[MaxCount];
    private readonly float[] _times = new float[MaxCount];
    private int _count;

    private readonly TimeSpan _timeCompensation = TimeSpan.FromSeconds(0.2f);

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null || _xformSystem is null && !_entMan.TrySystem(out _xformSystem))
            return false;

        var query = _entMan.EntityQueryEnumerator<RMCExplosionShockWaveComponent, TransformComponent>();

        _count = 0;

        while (query.MoveNext(out var uid, out var distortion, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var mapPos = _xformSystem.GetWorldPosition(uid);

            var tempCoords = args.Viewport.WorldToLocal(mapPos);

            // normalized coords, 0 - 1 plane. This is pure hell, we subtract 1 because fragment calculates from the bottom and local goes from the top of the viewport
            tempCoords.Y = 1 - (tempCoords.Y / args.Viewport.Size.Y);
            tempCoords.X /= args.Viewport.Size.X;

            var currentTime = (float)(_timing.CurTime - distortion.CreationTime - _timeCompensation).TotalSeconds;
            currentTime = Math.Clamp(currentTime, 0.1f, float.MaxValue);

            _positions[_count] = tempCoords;
            _falloffPower[_count] = distortion.FalloffPower ?? 20f; // Sunrise edit - фолбек
            _sharpness[_count] = distortion.Sharpness;
            _width[_count] = distortion.Width ?? 0.8f;
            _times[_count] = currentTime;
            _count++;

            if (_count == MaxCount)
                break;
        }

        return _count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        _shader?.SetParameter("renderScale", args.Viewport.RenderScale * args.Viewport.Eye.Scale);
        _shader?.SetParameter("count", _count);
        _shader?.SetParameter("position", _positions);
        _shader?.SetParameter("falloffPower", _falloffPower);
        _shader?.SetParameter("sharpness", _sharpness);
        _shader?.SetParameter("width", _width);
        _shader?.SetParameter("time", _times);
        _shader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }
}

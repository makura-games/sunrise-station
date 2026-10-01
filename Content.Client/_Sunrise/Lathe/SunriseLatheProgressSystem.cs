using Content.Shared._Sunrise.Lathe;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Lathe;

public sealed class SunriseLatheProgressSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ResPath ProgressRsi = new("_Sunrise/Interface/Machines/mechanical_progress_bar.rsi");
    private static readonly RSI.StateId BaseState = new("base");
    private static readonly RSI.StateId InterruptedState = new("production_interrupted");
    private static readonly RSI.StateId[] ProgressStates =
    [
        new("progress_0"),
        new("progress_10"),
        new("progress_20"),
        new("progress_30"),
        new("progress_40"),
        new("progress_50"),
        new("progress_60"),
        new("progress_70"),
        new("progress_80"),
        new("progress_90"),
        new("progress_100"),
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseLatheProgressComponent, ComponentStartup>(OnProgressStartup);
        SubscribeLocalEvent<SunriseLatheProgressComponent, ComponentShutdown>(OnProgressShutdown);
        SubscribeLocalEvent<SunriseLatheProgressComponent, AfterAutoHandleStateEvent>(OnProgressState);
    }

    private void OnProgressStartup(Entity<SunriseLatheProgressComponent> ent, ref ComponentStartup args)
    {
        UpdateVisual(ent);
    }

    private void OnProgressShutdown(Entity<SunriseLatheProgressComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.RemoveLayer((ent.Owner, sprite), SunriseLatheProgressVisualLayers.Progress, false);
        _sprite.RemoveLayer((ent.Owner, sprite), SunriseLatheProgressVisualLayers.Base, false);
    }

    private void OnProgressState(Entity<SunriseLatheProgressComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisual(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SunriseLatheProgressComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var progress, out var sprite))
        {
            UpdateVisual((uid, progress), sprite);
        }
    }

    private void UpdateVisual(Entity<SunriseLatheProgressComponent> ent, SpriteComponent? sprite = null)
    {
        if (!Resolve(ent.Owner, ref sprite, false))
            return;

        Entity<SpriteComponent> spriteEnt = (ent.Owner, sprite);
        var nullableSpriteEnt = spriteEnt.AsNullable();
        var baseLayer = EnsureLayer(nullableSpriteEnt, SunriseLatheProgressVisualLayers.Base, BaseState);
        var progressLayer = EnsureLayer(nullableSpriteEnt, SunriseLatheProgressVisualLayers.Progress, ProgressStates[0]);

        var interrupted = ent.Comp.State == SunriseLatheProgressState.Interrupted;
        _sprite.LayerSetVisible(nullableSpriteEnt, baseLayer, !interrupted);

        var desiredState = interrupted
            ? InterruptedState
            : ProgressStates[GetProgressStep(ent.Comp)];

        if (_sprite.LayerGetRsiState(nullableSpriteEnt, progressLayer) != desiredState)
            _sprite.LayerSetRsiState(nullableSpriteEnt, progressLayer, desiredState);
    }

    private int EnsureLayer(
        Entity<SpriteComponent?> ent,
        SunriseLatheProgressVisualLayers layerKey,
        RSI.StateId initialState)
    {
        if (_sprite.LayerMapTryGet(ent, layerKey, out var layer, false))
            return layer;

        layer = _sprite.AddRsiLayer(ent, initialState, ProgressRsi);
        _sprite.LayerMapSet(ent, layerKey, layer);
        return layer;
    }

    private int GetProgressStep(SunriseLatheProgressComponent progress)
    {
        var duration = progress.EndTime - progress.StartTime;
        if (duration <= TimeSpan.Zero)
            return ProgressStates.Length - 1;

        var elapsed = _timing.CurTime - progress.StartTime;
        var ratio = Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0d, 1d);
        return (int) Math.Floor(ratio * (ProgressStates.Length - 1));
    }

    private enum SunriseLatheProgressVisualLayers : byte
    {
        Base,
        Progress,
    }
}

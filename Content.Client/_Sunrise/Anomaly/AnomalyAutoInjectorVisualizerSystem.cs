// TODO: создана временная визуализация, т.к. визуализатор medipen не подходит из-за метаболизма. В будущем стоит сделать с системой визуализации medipen.
using Content.Shared._Sunrise.Anomaly.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._Sunrise.Anomaly;

public sealed partial class AnomalyAutoInjectorVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _spriteSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UsedAnomalyAutoInjectorComponent, ComponentStartup>(OnUsedStartup);
        SubscribeLocalEvent<UsedAnomalyAutoInjectorComponent, ComponentShutdown>(OnUsedShutdown);
    }

    private void OnUsedStartup(EntityUid uid, UsedAnomalyAutoInjectorComponent comp, ComponentStartup args)
    {
        SetSpriteState(uid, comp.SpriteStateEmpty, comp.SpriteLayer);
    }

    private void OnUsedShutdown(EntityUid uid, UsedAnomalyAutoInjectorComponent comp, ComponentShutdown args)
    {
        SetSpriteState(uid, comp.SpriteStateFull, comp.SpriteLayer);
    }

    private void SetSpriteState(EntityUid uid, string state, AnomalyAutoInjectorVisualLayers layer)
    {
        _spriteSystem.LayerSetRsiState(uid, layer, state);
    }
}

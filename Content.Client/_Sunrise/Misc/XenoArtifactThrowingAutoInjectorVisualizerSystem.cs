using Content.Shared._Sunrise.Misc;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._Sunrise.Misc;

public sealed partial class XenoArtifactThrowingAutoInjectorVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _spriteSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UsedXenoArtifactThrowingAutoInjectorComponent, ComponentStartup>(OnUsedStartup);
        SubscribeLocalEvent<UsedXenoArtifactThrowingAutoInjectorComponent, ComponentShutdown>(OnUsedShutdown);
    }

    private void OnUsedStartup(EntityUid uid, UsedXenoArtifactThrowingAutoInjectorComponent comp, ComponentStartup args)
    {
        SetSpriteState(uid, comp.SpriteStateEmpty, comp.SpriteLayer);
    }

    private void OnUsedShutdown(EntityUid uid, UsedXenoArtifactThrowingAutoInjectorComponent comp, ComponentShutdown args)
    {
        SetSpriteState(uid, comp.SpriteStateFull, comp.SpriteLayer);
    }

    private void SetSpriteState(EntityUid uid, string state, XenoArtifactThrowingAutoInjectorVisualLayers layer)
    {
        _spriteSystem.LayerSetRsiState(uid, layer, state);
    }
}

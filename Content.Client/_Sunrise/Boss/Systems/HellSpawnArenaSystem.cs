using Content.Shared._Sunrise.Boss.Components;
using Content.Shared._Sunrise.Boss.Systems;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Boss.Systems;

/// <inheritdoc />
public sealed class HellSpawnArenaSystem : SharedHellSpawnArenaSystem
{
    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HellSpawnArenaTransferTargetComponent, ComponentInit>(OnApplyUnshadedShader);
        SubscribeLocalEvent<HellSpawnArenaTransferTargetComponent, ComponentShutdown>(OnRemoveUnshadedShader);
    }

    private void OnApplyUnshadedShader(EntityUid uid, HellSpawnArenaTransferTargetComponent comp, ComponentInit args)
    {
        ApplyUnshadedShader(uid);
    }

    private void OnRemoveUnshadedShader(EntityUid uid,
        HellSpawnArenaTransferTargetComponent comp,
        ComponentShutdown args)
    {
        RemoveUnshadedShader(uid);
    }

    public void ApplyUnshadedShader(EntityUid entity)
    {
        if (!TryComp<AppearanceComponent>(entity, out var appearanceComponent))
            return;
        if (!TryComp<SpriteComponent>(entity, out var spriteComponent))
            return;

        var i = 0;
        foreach (var _ in spriteComponent.AllLayers)
        {
            spriteComponent.LayerSetShader(i, "unshaded");
            i++;
        }
    }

    public void RemoveUnshadedShader(EntityUid entity)
    {
        if (!TryComp<AppearanceComponent>(entity, out var appearanceComponent))
            return;
        if (!TryComp<SpriteComponent>(entity, out var spriteComponent))
            return;

        var i = 0;
        foreach (var _ in spriteComponent.AllLayers)
        {
            spriteComponent.LayerSetShader(i, null, null);
            i++;
        }
    }
}

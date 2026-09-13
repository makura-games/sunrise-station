using Content.Shared._Sunrise;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Body;

public sealed partial class VisualBodySystem
{
    private void InitializeSunriseBodyTypes()
    {
        SubscribeLocalEvent<SunriseHumanoidBaseLayersComponent, ComponentStartup>(OnSunriseBaseLayersStartup);
        SubscribeLocalEvent<SunriseHumanoidBaseLayersComponent, AfterAutoHandleStateEvent>(OnSunriseBaseLayersState);
    }

    private void OnSunriseBaseLayersStartup(Entity<SunriseHumanoidBaseLayersComponent> ent, ref ComponentStartup args)
    {
        RefreshBodyTypeVisuals(ent.Owner);
    }

    private void OnSunriseBaseLayersState(Entity<SunriseHumanoidBaseLayersComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshBodyTypeVisuals(ent.Owner);
    }

    public void RefreshBodyTypeVisuals(EntityUid target)
    {
        if (TryComp<BodyComponent>(target, out var body) &&
            body.Organs is not null)
        {
            foreach (var organ in body.Organs.ContainedEntities)
            {
                if (TryComp<VisualOrganComponent>(organ, out var visualOrgan))
                    ApplyVisual((organ, visualOrgan), target);

                if (!TryComp<VisualOrganMarkingsComponent>(organ, out var visualOrganMarkings))
                    continue;

                RemoveMarkings((organ, visualOrganMarkings), target);
                ApplyMarkings((organ, visualOrganMarkings), target);
            }
        }
    }

    public void UpdateSunriseBodyTypeLayerData(Entity<VisualOrganComponent> ent, EntityUid target, ref PrototypeLayerData data)
    {
        if (ent.Comp.Layer is not HumanoidVisualLayers layer)
            return;

        var sex = GetSex(target);
        var hasOverride = false;
        var mergedData = data;

        if (TryComp<SunriseHumanoidProfileComponent>(target, out var profile) &&
            _prototype.TryIndex(profile.BodyType, out var bodyType) &&
            bodyType.TryGetLayer(layer, sex, out var bodyTypeLayerData))
        {
            mergedData = SunrisePrototypeLayerData.Merge(mergedData, bodyTypeLayerData);
            hasOverride = true;
        }

        if (TryComp<SunriseHumanoidBaseLayersComponent>(target, out var baseLayers) &&
            baseLayers.CustomBaseLayers.TryGetValue(layer, out var custom))
        {
            if (custom.Data is not null)
            {
                mergedData = SunrisePrototypeLayerData.Merge(mergedData, custom.Data);
                hasOverride = true;
            }

            if (custom.Color is { } color)
            {
                if (!hasOverride)
                    mergedData = SunrisePrototypeLayerData.Clone(mergedData);

                mergedData.Color = color;
                hasOverride = true;
            }
        }

        if (hasOverride)
            data = mergedData;
    }

    private void UpdateSunriseBodyTypeLayerVisibility(
        Entity<VisualOrganComponent> ent,
        EntityUid target,
        int index,
        bool visible)
    {
        if (ent.Comp.Layer is not HumanoidVisualLayers layer)
            return;

        _sprite.LayerSetVisible(target, index, IsSunriseLayerVisible(target, layer, visible));
    }

    private bool IsSunriseLayerVisible(EntityUid target, HumanoidVisualLayers layer, bool visible)
    {
        if (!visible)
            return false;

        if (TryComp<SunriseHumanoidBaseLayersComponent>(target, out var baseLayers) &&
            baseLayers.PermanentlyHidden.Contains(layer))
        {
            return false;
        }

        if (TryComp<HideableHumanoidLayersComponent>(target, out var hideable) &&
            hideable.HiddenLayers.ContainsKey(layer))
        {
            return false;
        }

        return true;
    }

    private string? GetBodyTypeVisualKey(EntityUid uid)
    {
        if (!TryComp<SunriseHumanoidProfileComponent>(uid, out var profile) ||
            !_prototype.TryIndex<BodyTypePrototype>(profile.BodyType, out var bodyType))
        {
            return null;
        }

        return bodyType.VisualKey;
    }

    private Sex GetSex(EntityUid uid)
    {
        return TryComp<HumanoidProfileComponent>(uid, out var profile)
            ? profile.Sex
            : Sex.Unsexed;
    }
}

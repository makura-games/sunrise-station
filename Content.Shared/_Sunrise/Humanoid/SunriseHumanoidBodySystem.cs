using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Humanoid;

public sealed partial class SunriseHumanoidBodySystem : EntitySystem
{
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public void SetSkinColor(EntityUid uid, Color skinColor, bool sync = true)
    {
        var profile = GetOrganProfile(uid);
        profile.SkinColor = skinColor;

        if (!sync)
            return;

        _visualBody.ApplyProfile(uid, profile);
        ResyncMarkings(uid);
    }

    public void SetEyeColor(EntityUid uid, Color eyeColor, bool sync = true)
    {
        var profile = GetOrganProfile(uid);
        profile.EyeColor = eyeColor;

        if (!sync)
            return;

        _visualBody.ApplyProfile(uid, profile);
        ResyncMarkings(uid);
    }

    public void CloneHumanoidProfile(EntityUid source, EntityUid target)
    {
        if (!TryComp<HumanoidProfileComponent>(source, out var sourceProfile))
            return;

        var targetProfile = EnsureComp<HumanoidProfileComponent>(target);
        targetProfile.Species = sourceProfile.Species;
        targetProfile.Sex = sourceProfile.Sex;
        targetProfile.Gender = sourceProfile.Gender;
        targetProfile.Age = sourceProfile.Age;
        Dirty(target, targetProfile);
    }

    public void CloneBaseLayers(EntityUid source, EntityUid target)
    {
        if (!TryComp<SunriseHumanoidBaseLayersComponent>(source, out var sourceLayers))
            return;

        var targetLayers = EnsureComp<SunriseHumanoidBaseLayersComponent>(target);
        targetLayers.PermanentlyHidden = new(sourceLayers.PermanentlyHidden);
        targetLayers.CustomBaseLayers = CloneCustomBaseLayers(sourceLayers.CustomBaseLayers);
        Dirty(target, targetLayers);
    }

    public void SetLayersVisibility(
        EntityUid uid,
        IEnumerable<HumanoidVisualLayers> layers,
        bool visible,
        SunriseHumanoidBaseLayersComponent? baseLayers = null)
    {
        baseLayers ??= EnsureComp<SunriseHumanoidBaseLayersComponent>(uid);

        var dirty = false;
        foreach (var layer in layers)
        {
            if (visible)
                dirty |= baseLayers.PermanentlyHidden.Remove(layer);
            else
                dirty |= baseLayers.PermanentlyHidden.Add(layer);

            var ev = new HumanoidLayerVisibilityChangedEvent(layer, IsLayerVisible(uid, layer, baseLayers: baseLayers));
            RaiseLocalEvent(uid, ref ev);
        }

        if (dirty)
            Dirty(uid, baseLayers);
    }

    public void SetLayerVisibility(
        EntityUid uid,
        HumanoidVisualLayers layer,
        bool visible,
        SlotFlags? source = null,
        SunriseHumanoidBaseLayersComponent? baseLayers = null)
    {
        SetLayersVisibility(uid, [layer], visible, baseLayers);
    }

    public bool IsLayerVisible(
        EntityUid uid,
        HumanoidVisualLayers layer,
        HideableHumanoidLayersComponent? hideable = null,
        SunriseHumanoidBaseLayersComponent? baseLayers = null)
    {
        if (baseLayers is null &&
            TryComp(uid, out SunriseHumanoidBaseLayersComponent? resolvedBaseLayers))
        {
            baseLayers = resolvedBaseLayers;
        }

        if (baseLayers?.PermanentlyHidden.Contains(layer) == true)
            return false;

        if (hideable is null &&
            TryComp(uid, out HideableHumanoidLayersComponent? resolvedHideable))
        {
            hideable = resolvedHideable;
        }

        return hideable?.HiddenLayers.ContainsKey(layer) != true;
    }

    public void SetBaseLayerData(
        EntityUid uid,
        HumanoidVisualLayers layer,
        PrototypeLayerData? data,
        bool sync = true,
        SunriseHumanoidBaseLayersComponent? baseLayers = null)
    {
        baseLayers ??= EnsureComp<SunriseHumanoidBaseLayersComponent>(uid);

        baseLayers.CustomBaseLayers.TryGetValue(layer, out var current);
        var layerData = data is not null ? SunrisePrototypeLayerData.Clone(data) : null;
        SetCustomBaseLayer(baseLayers, layer, new CustomBaseLayerInfo(layerData, current.Color));

        if (sync)
            Dirty(uid, baseLayers);
    }

    public void SetBaseLayerColor(
        EntityUid uid,
        HumanoidVisualLayers layer,
        Color color,
        bool sync = true,
        SunriseHumanoidBaseLayersComponent? baseLayers = null)
    {
        baseLayers ??= EnsureComp<SunriseHumanoidBaseLayersComponent>(uid);

        baseLayers.CustomBaseLayers.TryGetValue(layer, out var current);
        var layerData = current.Data is not null ? SunrisePrototypeLayerData.Clone(current.Data) : null;
        SetCustomBaseLayer(baseLayers, layer, new CustomBaseLayerInfo(layerData, color));

        if (sync)
            Dirty(uid, baseLayers);
    }

    public ProtoId<SpeciesPrototype> GetSpecies(EntityUid uid)
    {
        return TryComp<HumanoidProfileComponent>(uid, out var profile)
            ? profile.Species
            : SunriseHumanoidProfileDefaults.DefaultSpecies;
    }

    public Sex GetSex(EntityUid uid)
    {
        return TryComp<HumanoidProfileComponent>(uid, out var profile)
            ? profile.Sex
            : Sex.Unsexed;
    }

    public OrganProfileData GetOrganProfile(EntityUid uid)
    {
        if (_visualBody.TryGatherMarkingsData(uid, null, out var profiles, out _, out _) &&
            profiles.Count > 0)
        {
            if (profiles.TryGetValue(new ProtoId<OrganCategoryPrototype>("Torso"), out var torsoProfile))
                return torsoProfile;

            if (profiles.TryGetValue(new ProtoId<OrganCategoryPrototype>("Head"), out var headProfile))
                return headProfile;

            return profiles.Values.First();
        }

        return new OrganProfileData
        {
            Sex = GetSex(uid),
            SkinColor = Color.FromHex("#C0967F"),
            EyeColor = Color.Brown,
        };
    }

    public Color GetSkinColor(EntityUid uid)
    {
        return GetOrganProfile(uid).SkinColor;
    }

    public Color GetEyeColor(EntityUid uid)
    {
        return GetOrganProfile(uid).EyeColor;
    }

    private void ResyncMarkings(EntityUid uid)
    {
        if (!_visualBody.TryGatherMarkingsData(uid, null, out _, out _, out var applied))
            return;

        _visualBody.ApplyMarkings(uid, CloneMarkings(applied));
    }

    private static Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> CloneMarkings(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> source)
    {
        var clone = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>(source.Count);

        foreach (var (organ, layers) in source)
        {
            var layerClone = new Dictionary<HumanoidVisualLayers, List<Marking>>(layers.Count);
            foreach (var (layer, markings) in layers)
            {
                var markingClone = new List<Marking>(markings.Count);
                foreach (var marking in markings)
                    markingClone.Add(marking.DeepClone());

                layerClone[layer] = markingClone;
            }

            clone[organ] = layerClone;
        }

        return clone;
    }

    private static Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CloneCustomBaseLayers(
        Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> source)
    {
        var clone = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(source.Count);

        foreach (var (layer, info) in source)
        {
            clone[layer] = new CustomBaseLayerInfo(
                info.Data is not null ? SunrisePrototypeLayerData.Clone(info.Data) : null,
                info.Color);
        }

        return clone;
    }

    private static void SetCustomBaseLayer(
        SunriseHumanoidBaseLayersComponent baseLayers,
        HumanoidVisualLayers layer,
        CustomBaseLayerInfo info)
    {
        if (info.Data is null && info.Color is null)
        {
            baseLayers.CustomBaseLayers.Remove(layer);
            return;
        }

        baseLayers.CustomBaseLayers[layer] = info;
    }
}

using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Humanoid;

public sealed partial class SunriseHumanoidMarkingSystem : EntitySystem
{
    [Dependency] private MarkingManager _marking = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public bool TryGetLayerMarkings(EntityUid uid, HumanoidVisualLayers layer, out List<Marking> markings)
    {
        markings = [];

        if (!_visualBody.TryGatherMarkingsData(uid, null, out _, out _, out var applied))
            return false;

        foreach (var layerMap in applied.Values)
        {
            if (!layerMap.TryGetValue(layer, out var layerMarkings))
                continue;

            foreach (var marking in layerMarkings)
                markings.Add(marking.DeepClone());

            return true;
        }

        return false;
    }

    public bool TryGetLayerLimit(EntityUid uid, HumanoidVisualLayers layer, out int limit)
    {
        limit = 0;

        if (!TryGetLayerMarkingData(uid, layer, out var data))
            return false;

        if (!_prototype.TryIndex(data.Group, out var group))
            return false;

        limit = group.Limits.TryGetValue(layer, out var layerLimit)
            ? layerLimit.Limit
            : -1;
        return true;
    }

    public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByLayerAndSpecies(
        HumanoidVisualLayers layer,
        ProtoId<SpeciesPrototype> species,
        Sex sex)
    {
        var markingData = _marking.GetMarkingData(species);
        foreach (var data in markingData.Values)
        {
            if (!data.Layers.Contains(layer))
                continue;

            return _marking.MarkingsByLayerAndGroupAndSex(layer, data.Group, sex);
        }

        return new Dictionary<string, MarkingPrototype>();
    }

    public bool SetMarkingId(EntityUid uid, HumanoidVisualLayers layer, int index, string markingId)
    {
        if (!_prototype.TryIndex<MarkingPrototype>(markingId, out var prototype) ||
            prototype.BodyPart != layer)
        {
            return false;
        }

        if (!TryGetLayerMarkingsForEdit(uid, layer, out var markings, out var layerMarkings))
            return false;

        if (index < 0 || index >= layerMarkings.Count)
            return false;

        var oldMarking = layerMarkings[index];
        var newMarking = prototype.AsMarking();
        newMarking.Forced = oldMarking.Forced;
        CopyMarkingStyle(oldMarking, ref newMarking);
        layerMarkings[index] = newMarking;

        _visualBody.ApplyMarkings(uid, markings);
        return true;
    }

    public bool SetMarkingColor(EntityUid uid, HumanoidVisualLayers layer, int index, IReadOnlyList<Color> colors)
    {
        if (!TryGetLayerMarkingsForEdit(uid, layer, out var markings, out var layerMarkings))
            return false;

        if (index < 0 || index >= layerMarkings.Count)
            return false;

        var marking = layerMarkings[index].DeepClone();
        var count = Math.Min(colors.Count, marking.MarkingColors.Count);
        for (var i = 0; i < count; i++)
            marking.SetColor(i, colors[i]);

        layerMarkings[index] = marking;
        _visualBody.ApplyMarkings(uid, markings);
        return true;
    }

    public bool SetAllMarkingColors(EntityUid uid, HumanoidVisualLayers layer, Color color)
    {
        if (!TryGetLayerMarkingsForEdit(uid, layer, out var markings, out var layerMarkings))
            return false;

        if (layerMarkings.Count == 0)
            return false;

        for (var i = 0; i < layerMarkings.Count; i++)
        {
            var marking = layerMarkings[i].DeepClone();
            marking.SetColor(color);
            layerMarkings[i] = marking;
        }

        _visualBody.ApplyMarkings(uid, markings);
        return true;
    }

    public bool AddMarking(EntityUid uid, string markingId, Color? color = null, bool forced = false)
    {
        if (!_prototype.TryIndex<MarkingPrototype>(markingId, out var prototype))
            return false;

        return AddMarking(uid, prototype.BodyPart, markingId, color, forced);
    }

    public bool AddMarking(EntityUid uid, HumanoidVisualLayers layer, string markingId, Color? color = null, bool forced = false)
    {
        if (!_prototype.TryIndex<MarkingPrototype>(markingId, out var prototype) ||
            prototype.BodyPart != layer)
        {
            return false;
        }

        if (!TryGetLayerMarkingsForEdit(uid, layer, out var markings, out var layerMarkings))
            return false;

        if (!forced && TryGetLayerLimit(uid, layer, out var limit) && limit >= 0 && layerMarkings.Count >= limit)
            return false;

        var marking = prototype.AsMarking();
        marking.Forced = forced;

        if (color is { } selectedColor)
            marking.SetColor(selectedColor);

        layerMarkings.Add(marking);
        _visualBody.ApplyMarkings(uid, markings);
        return true;
    }

    public bool AddFirstAvailableMarking(EntityUid uid, HumanoidVisualLayers layer, Color? color = null)
    {
        if (!TryGetLayerMarkingData(uid, layer, out var data))
            return false;

        var sex = TryComp<HumanoidProfileComponent>(uid, out var profile)
            ? profile.Sex
            : Sex.Unsexed;

        foreach (var marking in _marking.MarkingsByLayerAndGroupAndSex(layer, data.Group, sex).Values)
        {
            if (AddMarking(uid, layer, marking.ID, color))
                return true;
        }

        return false;
    }

    public bool RemoveMarking(EntityUid uid, HumanoidVisualLayers layer, int index)
    {
        if (!TryGetLayerMarkingsForEdit(uid, layer, out var markings, out var layerMarkings))
            return false;

        if (index < 0 || index >= layerMarkings.Count)
            return false;

        layerMarkings.RemoveAt(index);
        _visualBody.ApplyMarkings(uid, markings);
        return true;
    }

    public bool RemoveMarking(EntityUid uid, string markingId)
    {
        if (!_visualBody.TryGatherMarkingsData(uid, null, out _, out _, out var applied))
            return false;

        var markings = CloneMarkings(applied);
        foreach (var layerMap in markings.Values)
        {
            foreach (var layerMarkings in layerMap.Values)
            {
                for (var i = 0; i < layerMarkings.Count; i++)
                {
                    if (layerMarkings[i].MarkingId != markingId)
                        continue;

                    layerMarkings.RemoveAt(i);
                    _visualBody.ApplyMarkings(uid, markings);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetLayerMarkingData(
        EntityUid uid,
        HumanoidVisualLayers layer,
        out OrganMarkingData data)
    {
        data = default;

        if (!_visualBody.TryGatherMarkingsData(uid, null, out _, out var markingData, out _))
            return false;

        foreach (var organData in markingData.Values)
        {
            if (!organData.Layers.Contains(layer))
                continue;

            data = organData;
            return true;
        }

        return false;
    }

    private bool TryGetLayerMarkingsForEdit(
        EntityUid uid,
        HumanoidVisualLayers layer,
        out Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings,
        out List<Marking> layerMarkings)
    {
        markings = new();
        layerMarkings = [];

        if (!_visualBody.TryGatherMarkingsData(uid, null, out _, out var markingData, out var applied))
            return false;

        markings = CloneMarkings(applied);

        foreach (var (organ, data) in markingData)
        {
            if (!data.Layers.Contains(layer))
                continue;

            if (!markings.TryGetValue(organ, out var organMarkings))
            {
                organMarkings = new Dictionary<HumanoidVisualLayers, List<Marking>>();
                markings[organ] = organMarkings;
            }

            if (!organMarkings.TryGetValue(layer, out var existingLayerMarkings))
            {
                existingLayerMarkings = [];
                organMarkings[layer] = existingLayerMarkings;
            }

            layerMarkings = existingLayerMarkings;
            return true;
        }

        return false;
    }

    private static void CopyMarkingStyle(Marking source, ref Marking target)
    {
        var colorCount = Math.Min(source.MarkingColors.Count, target.MarkingColors.Count);
        for (var i = 0; i < colorCount; i++)
            target.SetColor(i, source.MarkingColors[i]);

        var effectCount = Math.Min(source.MarkingEffects.Count, target.MarkingEffects.Count);
        for (var i = 0; i < effectCount; i++)
            target.SetMarkingEffect(i, source.MarkingEffects[i].Clone());
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
}

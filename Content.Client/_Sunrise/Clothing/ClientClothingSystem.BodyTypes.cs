using System.Diagnostics.CodeAnalysis;
using Content.Shared._Sunrise;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Clothing.Components;
using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Clothing;

public sealed partial class ClientClothingSystem
{
    [Dependency] private TagSystem _tag = default!;

    private readonly string _hardsuitTag = "Hardsuit";

    public void RefreshEquipmentVisuals(Entity<InventoryComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        UpdateAllSlots(ent);
    }

    private void GetSunriseBodyTypeVisuals(
        EntityUid equipee,
        ClothingComponent clothing,
        string slot,
        ref List<PrototypeLayerData>? layers)
    {
        if (!TryGetSunriseBodyTypeVisualKey(equipee, out var bodyTypeVisualKey))
            return;

        if (clothing.ClothingVisuals.TryGetValue($"{slot}-{bodyTypeVisualKey}", out var bodyTypeLayers))
            layers = bodyTypeLayers;
    }

    private string GetSunriseBodyTypeState(EntityUid equipee, RSI rsi, string state)
    {
        if (!TryGetSunriseBodyTypeVisualKey(equipee, out var bodyTypeVisualKey))
            return state;

        var bodyTypeState = $"{state}-{bodyTypeVisualKey}";
        return rsi.TryGetState(bodyTypeState, out _) ? bodyTypeState : state;
    }

    private string? GetSunriseBodyTypeVisualKey(EntityUid equipee)
    {
        return TryGetSunriseBodyTypeVisualKey(equipee, out var bodyTypeVisualKey)
            ? bodyTypeVisualKey
            : null;
    }

    private bool TryGetSunriseBodyTypeVisualKey(EntityUid equipee, [NotNullWhen(true)] out string? bodyTypeVisualKey)
    {
        bodyTypeVisualKey = null;
        if (!TryComp(equipee, out SunriseHumanoidProfileComponent? profile) ||
            !ProtoMan.TryIndex(profile.BodyType, out var bodyType))
        {
            return false;
        }

        bodyTypeVisualKey = bodyType.VisualKey;
        return true;
    }

    private DisplacementData? GetSunriseBodyTypeDisplacement(
        EntityUid equipee,
        EntityUid equipment,
        string slot,
        InventoryComponent inventory,
        string? bodyTypeVisualKey,
        DisplacementData? fallback)
    {
        if (bodyTypeVisualKey is null ||
            !TryComp(equipee, out HumanoidProfileComponent? humanoid))
        {
            return fallback;
        }

        var sexDisplacements = humanoid.Sex switch
        {
            Sex.Male => inventory.MaleDisplacements,
            Sex.Female => inventory.FemaleDisplacements,
            _ => null,
        };

        var displacement = sexDisplacements?.GetValueOrDefault($"{slot}-{bodyTypeVisualKey}")
                           ?? inventory.Displacements.GetValueOrDefault($"{slot}-{bodyTypeVisualKey}")
                           ?? fallback;

        if (!_tag.HasTag(equipment, _hardsuitTag))
            return displacement;

        return sexDisplacements?.GetValueOrDefault($"hardsuit-{bodyTypeVisualKey}")
               ?? inventory.Displacements.GetValueOrDefault($"hardsuit-{bodyTypeVisualKey}")
               ?? displacement;
    }
}

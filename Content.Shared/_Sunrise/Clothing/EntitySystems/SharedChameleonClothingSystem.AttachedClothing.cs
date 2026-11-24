using Content.Shared.Clothing.Components;
using Content.Shared.Contraband;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Partial-расширение системы Wizden находится в папке Sunrise.
namespace Content.Shared.Clothing.EntitySystems;

public abstract partial class SharedChameleonClothingSystem
{
    /// <summary>
    /// Updates attached clothing to match the selected chameleon outfit's clothing prototype.
    /// </summary>
    private void UpdateAttachedClothingVisuals(EntityUid uid, EntityPrototype prototype)
    {
        if (!TryComp<ToggleableClothingComponent>(uid, out var toggleable) ||
            toggleable.ClothingUid is not { } clothingUid)
        {
            return;
        }

        if (!prototype.TryComp<ToggleableClothingComponent>(out var prototypeToggleable, Factory) ||
            !ProtoMan.TryIndex(prototypeToggleable.ClothingPrototype, out var clothingPrototype))
        {
            return;
        }

        if (TryComp<ClothingComponent>(clothingUid, out var clothing) &&
            clothingPrototype.TryComp<ClothingComponent>(out var prototypeClothing, Factory))
        {
            _clothingSystem.CopyVisuals(clothingUid, prototypeClothing, clothing);
        }

        if (TryComp<AppearanceComponent>(clothingUid, out var appearance) &&
            clothingPrototype.TryComp<AppearanceComponent>(out var prototypeAppearance, Factory))
        {
            _appearance.AppendData(prototypeAppearance, (clothingUid, appearance));
        }

        if (TryComp(clothingUid, out MetaDataComponent? metadata))
        {
            _metaData.SetEntityName(clothingUid, clothingPrototype.Name, metadata);
            _metaData.SetEntityDescription(clothingUid, clothingPrototype.Description, metadata);
        }

        if (clothingPrototype.TryComp<ContrabandComponent>(out var prototypeContraband, Factory))
        {
            var contraband = EnsureComp<ContrabandComponent>(clothingUid);
            _contraband.CopyDetails(clothingUid, prototypeContraband, contraband);
        }
        else
        {
            RemComp<ContrabandComponent>(clothingUid);
        }
    }
}

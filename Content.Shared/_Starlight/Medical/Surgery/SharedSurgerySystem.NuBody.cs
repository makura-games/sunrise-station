using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Inventory;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Shared.Starlight.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    public static string GetSurgeryPart(OrganComponent? organ)
    {
        return GetSurgeryPart(organ?.Category?.Id);
    }

    public static string GetSurgeryPart(string? category)
    {
        return category switch
        {
            "Head" => "Head",
            "Torso" => "Torso",
            "ArmLeft" or "ArmRight" => "Arm",
            "HandLeft" or "HandRight" => "Hand",
            "LegLeft" or "LegRight" => "Leg",
            "FootLeft" or "FootRight" => "Foot",
            _ => "Other",
        };
    }

    public static bool IsSurgeryTarget(OrganComponent? organ)
    {
        return GetSurgeryPart(organ) != "Other";
    }

    public static int GetSurgeryTargetScore(OrganComponent? organ)
    {
        return organ?.Category?.Id switch
        {
            "Head" => 1,
            "Torso" => 2,
            "ArmLeft" => 3,
            "ArmRight" => 4,
            "HandLeft" => 5,
            "HandRight" => 6,
            "LegLeft" => 7,
            "LegRight" => 8,
            "FootLeft" => 9,
            "FootRight" => 10,
            _ => 11,
        };
    }

    public static SlotFlags GetSurgeryTargetSlots(string part)
    {
        return part switch
        {
            "Head" => SlotFlags.HEAD | SlotFlags.EYES,
            "Torso" => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            "Arm" => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            "Hand" => SlotFlags.GLOVES,
            "Leg" => SlotFlags.OUTERCLOTHING | SlotFlags.LEGS,
            "Foot" => SlotFlags.FEET,
            _ => SlotFlags.NONE,
        };
    }

    public static string? GetSlotCategory(string slot)
    {
        return slot switch
        {
            "head" => "Head",
            "left arm" => "ArmLeft",
            "right arm" => "ArmRight",
            "left hand" => "HandLeft",
            "right hand" => "HandRight",
            "left leg" => "LegLeft",
            "right leg" => "LegRight",
            "left foot" => "FootLeft",
            "right foot" => "FootRight",
            "liver" => "Liver",
            "kidneys" => "Kidneys",
            "stomach" => "Stomach",
            "lungs" => "Lungs",
            "heart" => "Heart",
            "eyes" => "Eyes",
            "tongue" => "Tongue",
            "brain" => "Brain",
            "appendix" => "Appendix",
            _ => null,
        };
    }

    public static string? GetAttachmentAnchorCategory(string slot)
    {
        return slot switch
        {
            "head" => "Torso",
            "left arm" => "Torso",
            "right arm" => "Torso",
            "left hand" => "ArmLeft",
            "right hand" => "ArmRight",
            "left leg" => "Torso",
            "right leg" => "Torso",
            "left foot" => "LegLeft",
            "right foot" => "LegRight",
            _ => null,
        };
    }

    public static bool IsCavitySlot(string? slot)
    {
        return slot == "cavity";
    }

    public bool TryGetBodyContainer(EntityUid body, out Container container)
    {
        container = default!;

        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs == null)
            return false;

        container = bodyComp.Organs;
        return true;
    }

    public static bool IsOrganCategory(OrganComponent organ, string category)
    {
        return organ.Category?.Id == category;
    }

    public bool TryFindBodyOrganByCategory(EntityUid body, string category, out EntityUid organ)
    {
        organ = EntityUid.Invalid;

        if (!TryGetBodyContainer(body, out var container))
            return false;

        foreach (var contained in container.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(contained, out var organComp) || !IsOrganCategory(organComp, category))
                continue;

            organ = contained;
            return true;
        }

        return false;
    }

    public bool HasBodyOrganCategory(EntityUid body, string category)
    {
        return TryFindBodyOrganByCategory(body, category, out _);
    }

    public bool TryFindBodyEntityWithComponent(EntityUid body, Type componentType, string? slot, out EntityUid found)
    {
        found = EntityUid.Invalid;

        if (!TryGetBodyContainer(body, out var container))
            return false;

        var category = slot == null || IsCavitySlot(slot)
            ? GetRequirementOrganCategory(componentType)
            : GetSlotCategory(slot);

        foreach (var contained in container.ContainedEntities)
        {
            if (IsCavitySlot(slot) && HasComp<OrganComponent>(contained))
                continue;

            if (category != null)
            {
                if (!TryComp<OrganComponent>(contained, out var organ) || !IsOrganCategory(organ, category))
                    continue;
            }
            else if (!HasComp(contained, componentType))
            {
                continue;
            }

            found = contained;
            return true;
        }

        return false;
    }

    public bool CanAttachToSlot(EntityUid body, EntityUid targetPart, string slot)
    {
        var category = GetSlotCategory(slot);
        var anchorCategory = GetAttachmentAnchorCategory(slot);

        if (category == null || anchorCategory == null || HasBodyOrganCategory(body, category))
            return false;

        return TryComp<OrganComponent>(targetPart, out var targetOrgan) &&
               IsOrganCategory(targetOrgan, anchorCategory);
    }

    public bool TryGetFreeAttachmentSlot(EntityUid body, EntityUid targetPart, out string slot)
    {
        foreach (var candidate in new[]
                 {
                     "head",
                     "left arm",
                     "right arm",
                     "left hand",
                     "right hand",
                     "left leg",
                     "right leg",
                     "left foot",
                     "right foot",
                 })
        {
            if (!CanAttachToSlot(body, targetPart, candidate))
                continue;

            slot = candidate;
            return true;
        }

        slot = string.Empty;
        return false;
    }

    public bool TryInsertIntoBody(EntityUid body, EntityUid entity)
    {
        return TryGetBodyContainer(body, out var container) && _containers.Insert(entity, container);
    }

    public bool TryRemoveFromBody(EntityUid body, EntityUid entity, EntityCoordinates? destination = null)
    {
        return TryGetBodyContainer(body, out var container) &&
               container.Contains(entity) &&
               _containers.Remove(entity, container, destination: destination);
    }

    // Sunrise added start - проверяем surgery-органы по категории, когда marker-компонент есть не на всех прототипах
    protected bool MatchesSurgeryRequirement(EntityUid entity, Type componentType)
    {
        if (GetRequirementOrganCategory(componentType) is not { } category)
            return HasComp(entity, componentType);

        return TryComp<OrganComponent>(entity, out var organ) && IsOrganCategory(organ, category);
    }

    private static string? GetRequirementOrganCategory(Type componentType)
    {
        if (componentType == typeof(OrganBrainComponent))
            return "Brain";

        if (componentType == typeof(OrganEyesComponent))
            return "Eyes";

        if (componentType == typeof(OrganTongueComponent))
            return "Tongue";

        if (componentType == typeof(OrganAppendixComponent))
            return "Appendix";

        if (componentType == typeof(OrganEarsComponent))
            return "Ears";

        if (componentType == typeof(LungComponent))
            return "Lungs";

        if (componentType == typeof(OrganHeartComponent))
            return "Heart";

        if (componentType == typeof(OrganStomachComponent))
            return "Stomach";

        if (componentType == typeof(OrganLiverComponent))
            return "Liver";

        if (componentType == typeof(OrganKidneysComponent))
            return "Kidneys";

        return null;
    }
    // Sunrise added end
}

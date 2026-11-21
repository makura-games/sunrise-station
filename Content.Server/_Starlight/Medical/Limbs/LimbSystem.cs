using System.Linq;
using System.Reflection;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared._Starlight.Medical.Limbs;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Starlight;
using Content.Shared.Starlight.Medical.Surgery;
using Robust.Server.Containers;
using Robust.Shared.Maths;

namespace Content.Server._Starlight.Medical.Limbs;

public sealed partial class LimbSystem : SharedLimbSystem
{
    [Dependency] private ContainerSystem _containers = default!;
    [Dependency] private SunriseHumanoidBodySystem _sunriseBody = default!;

    private static readonly MethodInfo? RaiseLocalEventRefMethod = typeof(LimbSystem)
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(m => m.Name == nameof(RaiseLocalEvent) && m.IsGenericMethodDefinition)
        .FirstOrDefault(m =>
        {
            var parameters = m.GetParameters();
            return parameters.Length == 3 &&
                   parameters[0].ParameterType == typeof(EntityUid) &&
                   parameters[1].ParameterType.IsByRef &&
                   parameters[2].ParameterType == typeof(bool);
        });

    public bool AttachLimb(Entity<BodyComponent> body, string slot, Entity<OrganComponent> limb)
    {
        if (body.Comp.Organs == null ||
            SharedSurgerySystem.GetSlotCategory(slot) is not { } category ||
            !IsOrganCategory(limb.Comp, category) ||
            HasBodyOrganCategory(body, category) ||
            !_containers.Insert(limb.Owner, body.Comp.Organs))
        {
            return false;
        }

        if (TryComp<HumanoidProfileComponent>(body.Owner, out var humanoid))
            AddLimbVisual((body.Owner, humanoid), limb);

        RaiseLimbAttachedEvents(body.Owner, limb.Owner);
        AddAttachedBodyParts(body, limb);
        return true;
    }

    public bool Amputate(Entity<BodyComponent> body, EntityUid limb)
    {
        if (body.Comp.Organs == null || !body.Comp.Organs.Contains(limb))
            return false;

        if (TryComp<OrganComponent>(limb, out var organ))
        {
            foreach (var childCategory in GetChildCategories(organ))
            {
                if (TryFindBodyOrganByCategory(body, childCategory, out var child) &&
                    !AmputateSingle(body, child))
                {
                    return false;
                }
            }
        }

        return AmputateSingle(body, limb);
    }

    public void ToggleLimbVisual(Entity<HumanoidProfileComponent> body, Entity<BaseLayerDataComponent, BaseLayerToggledDataComponent, OrganComponent> limb, bool toggled)
    {
        if (GetLayer(limb.Comp3) is not { } layer)
            return;

        _sunriseBody.SetBaseLayerData(body.Owner, layer, toggled ? limb.Comp2.Data : limb.Comp1.Data);
    }

    private bool AmputateSingle(Entity<BodyComponent> body, EntityUid limb)
    {
        if (body.Comp.Organs == null || !body.Comp.Organs.Contains(limb))
            return false;

        var destination = Transform(body).Coordinates;
        if (!_containers.Remove(limb, body.Comp.Organs, destination: destination))
            return false;

        if (TryComp<HumanoidProfileComponent>(body, out var humanoid) &&
            TryComp<OrganComponent>(limb, out var organ))
        {
            RemoveLimbVisual((body, humanoid), (limb, organ));
        }

        RaiseLimbRemovedEvents(body, limb);
        return true;
    }

    private void AddAttachedBodyParts(Entity<BodyComponent> body, Entity<OrganComponent> limb)
    {
        if (body.Comp.Organs == null || !TryComp<WithAttachedBodyPartsComponent>(limb, out var attached))
            return;

        foreach (var (slot, prototype) in attached.Parts)
        {
            var category = SharedSurgerySystem.GetSlotCategory(slot);
            if (category == null || HasBodyOrganCategory(body, category))
                continue;

            var child = Spawn(prototype, Transform(body).Coordinates);
            if (!TryComp<OrganComponent>(child, out var childOrgan) ||
                !IsOrganCategory(childOrgan, category) ||
                !_containers.Insert(child, body.Comp.Organs))
            {
                QueueDel(child);
                continue;
            }

            if (TryComp<HumanoidProfileComponent>(body, out var humanoid))
                AddLimbVisual((body, humanoid), (child, childOrgan));

            RaiseLimbAttachedEvents(body, child);
        }
    }

    private void AddLimbVisual(Entity<HumanoidProfileComponent?> body, Entity<OrganComponent> limb)
    {
        if (GetLayer(limb.Comp) is not { } layer)
            return;

        if (TryComp<BaseLayerDataComponent>(limb.Owner, out var baseLayer) && baseLayer.Data != null)
        {
            _sunriseBody.SetBaseLayerData(body.Owner, layer, baseLayer.Data);
        }

        _sunriseBody.SetLayersVisibility(body.Owner, [layer], true);
    }

    private void RemoveLimbVisual(Entity<HumanoidProfileComponent?> body, Entity<OrganComponent> limb)
    {
        if (GetLayer(limb.Comp) is not { } layer)
            return;

        _sunriseBody.SetLayersVisibility(body.Owner, [layer], false);
    }

    private bool TryFindBodyOrganByCategory(Entity<BodyComponent> body, string category, out EntityUid organ)
    {
        organ = EntityUid.Invalid;

        if (body.Comp.Organs == null)
            return false;

        foreach (var contained in body.Comp.Organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(contained, out var organComp) || !IsOrganCategory(organComp, category))
                continue;

            organ = contained;
            return true;
        }

        return false;
    }

    private bool HasBodyOrganCategory(Entity<BodyComponent> body, string category)
    {
        return TryFindBodyOrganByCategory(body, category, out _);
    }

    private static bool IsOrganCategory(OrganComponent organ, string category)
    {
        return organ.Category?.Id == category;
    }

    private static IEnumerable<string> GetChildCategories(OrganComponent organ)
    {
        switch (organ.Category?.Id)
        {
            case "ArmLeft":
                yield return "HandLeft";
                break;
            case "ArmRight":
                yield return "HandRight";
                break;
            case "LegLeft":
                yield return "FootLeft";
                break;
            case "LegRight":
                yield return "FootRight";
                break;
        }
    }

    private static HumanoidVisualLayers? GetLayer(OrganComponent organ)
    {
        return organ.Category?.Id switch
        {
            "Head" => HumanoidVisualLayers.Head,
            "Torso" => HumanoidVisualLayers.Chest,
            "ArmLeft" => HumanoidVisualLayers.LArm,
            "ArmRight" => HumanoidVisualLayers.RArm,
            "HandLeft" => HumanoidVisualLayers.LHand,
            "HandRight" => HumanoidVisualLayers.RHand,
            "LegLeft" => HumanoidVisualLayers.LLeg,
            "LegRight" => HumanoidVisualLayers.RLeg,
            "FootLeft" => HumanoidVisualLayers.LFoot,
            "FootRight" => HumanoidVisualLayers.RFoot,
            _ => null,
        };
    }

    private void RaiseLimbAttachedEvents(EntityUid body, EntityUid limb)
    {
        RaiseImplantableEvents(body, limb, typeof(LimbAttachedEvent<>));
    }

    private void RaiseLimbRemovedEvents(EntityUid body, EntityUid limb)
    {
        RaiseImplantableEvents(body, limb, typeof(LimbRemovedEvent<>));
    }

    private void RaiseImplantableEvents(EntityUid body, EntityUid limb, Type eventTypeDefinition)
    {
        if (RaiseLocalEventRefMethod == null)
            return;

        foreach (var comp in AllComps(limb))
        {
            if (comp is not IImplantable)
                continue;

            RaiseImplantableEvent(body, limb, comp, comp.GetType(), eventTypeDefinition);

            foreach (var face in comp.GetType().GetInterfaces().Where(typeof(IImplantable).IsAssignableFrom))
            {
                RaiseImplantableEvent(body, limb, comp, face, eventTypeDefinition);
            }
        }
    }

    private void RaiseImplantableEvent(EntityUid body, EntityUid limb, IComponent comp, Type componentType, Type eventTypeDefinition)
    {
        var eventType = eventTypeDefinition.MakeGenericType(componentType);
        var ev = Activator.CreateInstance(eventType, [limb, comp]);
        if (ev == null)
            return;

        var closedMethod = RaiseLocalEventRefMethod!.MakeGenericMethod(eventType);
        closedMethod.Invoke(this, [body, ev, false]);
    }
}

using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Starlight.Medical.Surgery.Effects.Step;
using Content.Shared.Starlight.Medical.Surgery.Events;

namespace Content.Shared.Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
public abstract partial class SharedSurgerySystem
{
    protected List<Type> _accents = [];

    private void InitializeConditions()
    {
        _accents = _reflectionManager.FindTypesWithAttribute<RegisterComponentAttribute>()
            .Where(type => type.Name.EndsWith("AccentComponent"))
            .ToList();

        SubscribeLocalEvent<SurgeryPartConditionComponent, SurgeryValidEvent>(OnPartConditionValid);
        SubscribeLocalEvent<SurgerySpeciesConditionComponent, SurgeryValidEvent>(OnSpeciesConditionValid);
        SubscribeLocalEvent<SurgeryOrganExistConditionComponent, SurgeryValidEvent>(OnOrganExistConditionValid);
        SubscribeLocalEvent<SurgeryOrganDontExistConditionComponent, SurgeryValidEvent>(OnOrganDontExistConditionValid);
        SubscribeLocalEvent<SurgeryAnyAccentConditionComponent, SurgeryValidEvent>(OnAnyAccentConditionValid);
        SubscribeLocalEvent<SurgeryAnyLimbSlotConditionComponent, SurgeryValidEvent>(OnAnyLimbSlotConditionValid);
        SubscribeLocalEvent<SurgeryLimbSlotConditionComponent, SurgeryValidEvent>(OnLimbSlotConditionValid);
    }

    private void OnOrganDontExistConditionValid(Entity<SurgeryOrganDontExistConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Organ?.Count != 1)
            return;

        var type = ent.Comp.Organ.Values.First().Component.GetType();
        if (TryFindBodyEntityWithComponent(args.Body, type, ent.Comp.Container, out _))
            args.Cancelled = true;
    }

    private void OnOrganExistConditionValid(Entity<SurgeryOrganExistConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Organ?.Count != 1)
            return;

        var type = ent.Comp.Organ.Values.First().Component.GetType();
        if (!TryFindBodyEntityWithComponent(args.Body, type, ent.Comp.Container, out _))
            args.Cancelled = true;
    }

    private void OnPartConditionValid(Entity<SurgeryPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Parts.Count == 0)
            return;

        if (!TryComp<OrganComponent>(args.Part, out var organ) ||
            !ent.Comp.Parts.Contains(GetSurgeryPart(organ)))
        {
            args.Cancelled = true;
        }
    }

    private void OnSpeciesConditionValid(Entity<SurgerySpeciesConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<HumanoidProfileComponent>(args.Body, out var humanoidAppearanceComponent))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.SpeciesBlacklist.Contains(humanoidAppearanceComponent.Species))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.SpeciesWhitelist.Count > 0 && !ent.Comp.SpeciesWhitelist.Contains(humanoidAppearanceComponent.Species))
        {
            args.Cancelled = true;
            return;
        }
    }

    private void OnAnyAccentConditionValid(Entity<SurgeryAnyAccentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        foreach (var accent in _accents)
        {
            if (HasComp(args.Body, accent))
                return;
        }

        args.Cancelled = true;
    }

    private void OnAnyLimbSlotConditionValid(Entity<SurgeryAnyLimbSlotConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (TryGetFreeAttachmentSlot(args.Body, args.Part, out var slot))
        {
            args.Suffix = slot;
            return;
        }

        args.Cancelled = true;
    }

    private void OnLimbSlotConditionValid(Entity<SurgeryLimbSlotConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (CanAttachToSlot(args.Body, args.Part, ent.Comp.Slot))
        {
            args.Suffix = ent.Comp.Slot;
            return;
        }

        args.Cancelled = true;
    }
}

using System.Linq;
using Content.Server._Starlight.Medical.Limbs;
using Content.Server.Administration.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Humanoid;
using Content.Shared.Starlight.Medical.Surgery;
using Content.Shared.Starlight.Medical.Surgery.Effects.Step;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Starlight.Medical.Surgery.Steps;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Content.Shared.Traits.Assorted;

namespace Content.Server.Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
//
//This file is already overloaded with responsibilities,
//it’s time to break its functionality into different systems.
//However, I don’t want to touch the official systems, so I need to come up with extensions for them.
public sealed partial class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private LimbSystem _limbSystem = default!;
    [Dependency] private StarlightEntitySystem _entity = default!;
    [Dependency] private SleepingSystem _sleeping = default!;

    public void InitializeSteps()
    {
        SubscribeLocalEvent<SurgeryStepBleedEffectComponent, SurgeryStepEvent>(OnStepBleedComplete);
        SubscribeLocalEvent<SurgeryClampBleedEffectComponent, SurgeryStepEvent>(OnStepClampBleedComplete);
        SubscribeLocalEvent<SurgeryStepEmoteEffectComponent, SurgeryStepEvent>(OnStepEmoteEffectComplete);
        SubscribeLocalEvent<SurgeryStepSpawnEffectComponent, SurgeryStepEvent>(OnStepSpawnComplete);

        SubscribeLocalEvent<SurgeryStepOrganExtractComponent, SurgeryStepEvent>(OnStepOrganExtractComplete);
        SubscribeLocalEvent<SurgeryStepOrganInsertComponent, SurgeryStepEvent>(OnStepOrganInsertComplete);

        SubscribeLocalEvent<SurgeryStepAttachLimbEffectComponent, SurgeryStepEvent>(OnStepAttachComplete);
        SubscribeLocalEvent<SurgeryStepAmputationEffectComponent, SurgeryStepEvent>(OnStepAmputationComplete);

        SubscribeLocalEvent<CustomLimbMarkerComponent, ComponentRemove>(CustomLimbRemoved);

        SubscribeLocalEvent<SurgeryRemoveAccentComponent, SurgeryStepEvent>(OnRemoveAccent);

    }

    private void OnStepAttachComplete(Entity<SurgeryStepAttachLimbEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_entity.TryGetSingleton(args.SurgeryProto, out var surgery)
            || !TryComp<SurgeryLimbSlotConditionComponent>(surgery, out var slotComp))
            return;

        OnStepAttachLimbComplete(ent, slotComp.Slot, ref args);
        if (slotComp.Slot != "head" && args.IsCancelled)
            OnStepAttachItemComplete(ent, slotComp.Slot, ref args);
    }

    private void OnStepBleedComplete(Entity<SurgeryStepBleedEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (ent.Comp.Damage is not null && TryComp<DamageableComponent>(args.Body, out var comp))
            _damageableSystem.TryChangeDamage(args.Body, ent.Comp.Damage);
        //todo add wound
    }

    private void OnStepClampBleedComplete(Entity<SurgeryClampBleedEffectComponent> ent, ref SurgeryStepEvent args)
    {
        //todo remove wound
    }

    private void OnStepOrganInsertComplete(Entity<SurgeryStepOrganInsertComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryGetSurgeryTool(ent.Owner, args.Tools, out var organId))
        {
            args.IsCancelled = true;
            return;
        }

        if (IsCavitySlot(ent.Comp.Slot))
        {
            args.IsCancelled = !TryInsertIntoBody(args.Body, organId);
            return;
        }

        if (!TryComp<OrganComponent>(organId, out var organComp))
        {
            args.IsCancelled = true;
            return;
        }

        var category = GetSlotCategory(ent.Comp.Slot);
        if (category != null && (!IsOrganCategory(organComp, category) || HasBodyOrganCategory(args.Body, category)))
        {
            args.IsCancelled = true;
            return;
        }

        if (!TryInsertIntoBody(args.Body, organId))
        {
            args.IsCancelled = true;
            return;
        }

        var ev = new SurgeryOrganImplantationCompleted(args.Body, args.Part, organId);
        RaiseLocalEvent(organId, ref ev);
    }

    private void OnStepOrganExtractComplete(Entity<SurgeryStepOrganExtractComponent> ent, ref SurgeryStepEvent args)
    {
        if (ent.Comp.Organ?.Count != 1)
            return;

        var type = ent.Comp.Organ.Values.First().Component.GetType();
        if (!TryFindBodyEntityWithComponent(args.Body, type, ent.Comp.Slot, out var organ))
        {
            args.IsCancelled = true;
            return;
        }

        var destination = Transform(args.Body).Coordinates;
        if (!TryRemoveFromBody(args.Body, organ, destination))
        {
            args.IsCancelled = true;
            return;
        }

        var ev = new SurgeryOrganExtracted(args.Body, args.Part, organ);
        RaiseLocalEvent(organ, ref ev);
    }

    private void OnRemoveAccent(Entity<SurgeryRemoveAccentComponent> ent, ref SurgeryStepEvent args)
    {
        foreach (var accent in _accents)
        {
            if (HasComp(args.Body, accent))
                RemCompDeferred(args.Body, accent);
        }
    }

    private void OnStepEmoteEffectComplete(Entity<SurgeryStepEmoteEffectComponent> ent, ref SurgeryStepEvent args)
    {

        if (!HasComp<PainNumbnessStatusEffectComponent>(args.Body) && !HasComp<SleepingComponent>(args.Body))
            _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote);
        else
            _sleeping.TryWaking(args.Body); // If the patient sleeping without n2o or reagents, wake them up.
    }

    private void OnStepSpawnComplete(Entity<SurgeryStepSpawnEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (TryComp(args.Body, out TransformComponent? xform))
            SpawnAtPosition(ent.Comp.Entity, xform.Coordinates);
    }

    private void OnStepAttachLimbComplete(Entity<SurgeryStepAttachLimbEffectComponent> _, string slot, ref SurgeryStepEvent args)
    {
        args.IsCancelled = true;

        var category = GetSlotCategory(slot);
        if (category == null || !CanAttachToSlot(args.Body, args.Part, slot))
            return;

        var limbId = args.Tools.FirstOrDefault(tool =>
            TryComp<OrganComponent>(tool, out var organ) &&
            IsOrganCategory(organ, category));

        if (limbId == default ||
            !TryComp<BodyComponent>(args.Body, out var body) ||
            !TryComp<OrganComponent>(limbId, out var limb) ||
            !_limbSystem.AttachLimb((args.Body, body), slot, (limbId, limb)))
        {
            return;
        }

        args.IsCancelled = false;
    }

    private void OnStepAttachItemComplete(Entity<SurgeryStepAttachLimbEffectComponent> ent, string slot, ref SurgeryStepEvent args)
    {
        args.IsCancelled = true;
    }

    private void OnStepAmputationComplete(Entity<SurgeryStepAmputationEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp<BodyComponent>(args.Body, out var body))
        {
            args.IsCancelled = true;
            return;
        }

        args.IsCancelled = !_limbSystem.Amputate((args.Body, body), args.Part);
    }

    private void CustomLimbRemoved(Entity<CustomLimbMarkerComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.VirtualPart is null)
            return;

        QueueDel(ent.Comp.VirtualPart.Value);
    }

    private bool TryGetSurgeryTool(EntityUid step, List<EntityUid> tools, out EntityUid tool)
    {
        tool = EntityUid.Invalid;

        if (!TryComp<SurgeryStepComponent>(step, out var stepComp) || stepComp.Tools == null)
        {
            tool = tools.FirstOrDefault();
            return tool != default;
        }

        foreach (var reg in stepComp.Tools.Values)
        {
            var type = reg.Component.GetType();
            tool = tools.FirstOrDefault(held => MatchesSurgeryRequirement(held, type));
            if (tool != default)
                return true;
        }

        tool = EntityUid.Invalid;
        return false;
    }
}

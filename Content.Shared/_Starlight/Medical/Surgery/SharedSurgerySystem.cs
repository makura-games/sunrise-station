using System;
using System.Linq;
using Content.Server.Administration.Systems;
using Content.Shared.Body;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Starlight.Medical.Surgery.Effects.Step;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Shared.Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
public abstract partial class SharedSurgerySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IReflectionManager _reflectionManager = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private StarlightEntitySystem _entitySystem = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();


        InitializeSteps();
        InitializeConditions();
    }

    public bool IsSurgeryValid
        (
            EntityUid body,
            EntityUid targetPart,
            EntProtoId surgery,
            EntProtoId stepId,
            out Entity<SurgeryComponent> surgeryEnt,
            out EntityUid partEnt,
            out EntityUid step
        )
    {
        surgeryEnt = default;
        partEnt = default;
        step = default;

        if (!HasComp<SurgeryTargetComponent>(body)
             || !IsLyingDown(body)
             || !Exists(targetPart)
             || !_entitySystem.TryGetSingleton(surgery, out var surgeryEntId)
             || !_entitySystem.TryEntity(surgeryEntId, out surgeryEnt)
             || !_entitySystem.TryGetSingleton(stepId, out step)
             || !surgeryEnt.Comp.Steps.Contains(stepId)
             || !TryComp<OrganComponent>(targetPart, out var organ)
             || !IsSurgeryTarget(organ))
            return false;

        partEnt = targetPart;

        var progress = EnsureComp<SurgeryProgressComponent>(targetPart);

        var ev = new SurgeryValidEvent(body, targetPart);

        if (!progress.StartedSurgeries.Contains(surgery))
        {
            RaiseLocalEvent(step, ref ev);
            RaiseLocalEvent(surgeryEntId, ref ev);
        }

        return !ev.Cancelled;
    }

    protected List<EntityUid> GetTools(EntityUid surgeon) => [.. _hands.EnumerateHeld(surgeon)];

    public bool IsLyingDown(EntityUid entity)
    {
        if (_standing.IsDown(entity))
            return true;

        if (HasComp<ItemComponent>(entity))
            return true;

        if (TryComp(entity, out BuckleComponent? buckle) &&
            TryComp(buckle.BuckledTo, out StrapComponent? strap))
        {
            var rotation = strap.Rotation;
            if (rotation.GetCardinalDir() is Direction.West or Direction.East)
                return true;
        }

        return false;
    }

    protected virtual void RefreshUI(EntityUid body)
    {
    }
}

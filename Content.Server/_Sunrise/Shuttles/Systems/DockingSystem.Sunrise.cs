using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Doors.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Robust.Shared.Log;
using Robust.Shared.Physics.Dynamics.Joints;

namespace Content.Server.Shuttles.Systems;

public sealed partial class DockingSystem
{
    [Dependency] private AirtightSystem _airtight = default!;
    [Dependency] private AirlockSystem _airlock = default!;
    [Dependency] private ILogManager _log = default!;

    private ISawmill? _logger;

    public override void Initialize()
    {
        base.Initialize();
        _logger = _log.GetSawmill("DockingSystem");
    }

    [SubscribeLocalEvent]
    private void OnSunriseDockingInit(Entity<DockingComponent> ent, ref ComponentInit args)
    {
        UpdateSunriseAirtight(ent);
    }

    [SubscribeLocalEvent]
    private void OnSunriseDoorStateChanged(Entity<DockingComponent> ent, ref DoorStateChangedEvent args)
    {
        if (!ent.Comp.Docked)
            SetSunriseAirblocked(ent, args.State is not (DoorState.Open or DoorState.Opening));
    }

    private void OnSunriseAnchorChanged(Entity<DockingComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            UpdateSunriseAirtight(ent);
    }

    [SubscribeLocalEvent]
    private void OnSunriseDocked(Entity<DockingComponent> ent, ref DockEvent args)
    {
        SetSunriseAirblocked(ent, true);
    }

    private void UpdateSunriseAirtight(EntityUid uid)
    {
        if (!TryComp<DoorComponent>(uid, out var door))
            return;

        SetSunriseAirblocked(uid, door.State is not (DoorState.Open or DoorState.Opening));
    }

    private void SetSunriseAirblocked(EntityUid uid, bool airblocked)
    {
        if (TryComp<AirtightComponent>(uid, out var airtight))
            _airtight.SetAirblocked((uid, airtight), airblocked);
    }

    private WeldJoint GetSunriseDockingJoint(
        Entity<DockingComponent> dockA,
        Entity<DockingComponent> dockB,
        EntityUid gridA,
        EntityUid gridB)
    {
        if (dockA.Comp.DockJointId != null && dockA.Comp.DockJointId == dockB.Comp.DockJointId)
            return _jointSystem.GetOrCreateWeldJoint(gridA, gridB, dockA.Comp.DockJointId);

        dockA.Comp.DockJointId = null;
        dockB.Comp.DockJointId = null;
        return _jointSystem.GetOrCreateWeldJoint(gridA, gridB, DockingJoint + dockA.Owner);
    }

    private bool CanSunriseUndock(EntityUid consoleUid)
    {
        var console = _console.GetDroneConsole(consoleUid);
        if (console == null)
            return false;

        return CanShuttleUndock(Transform(console.Value).GridUid);
    }

    private void UpdateSunriseUndockedDoor(Entity<DoorComponent> ent)
    {
        if (TryComp<AirlockComponent>(ent, out var airlock))
            _airlock.UpdateAutoClose((ent.Owner, airlock, ent.Comp));

        UpdateSunriseAirtight(ent);
    }
}

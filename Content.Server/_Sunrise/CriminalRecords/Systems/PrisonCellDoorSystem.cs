using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared._Sunrise.CriminalRecords.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.CriminalRecords.Systems;

/// <summary>
///     Handles locking and unlocking of prison cell doors via signals or direct API.
/// </summary>
public sealed partial class PrisonCellDoorSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;



    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrisonCellDoorComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<PrisonCellDoorComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(Entity<PrisonCellDoorComponent> ent, ref MapInitEvent args)
    {
        _deviceLink.EnsureSinkPorts(ent, "PrisonCellDoorLock", "PrisonCellDoorUnlock");
    }

    private void OnSignalReceived(Entity<PrisonCellDoorComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port == "PrisonCellDoorLock")
        {
            TryLockDoor(ent.Owner);
        }
        else if (args.Port == "PrisonCellDoorUnlock")
        {
            TryUnlockDoor(ent.Owner);
        }
    }

    public bool TryLockDoor(Entity<PrisonCellDoorComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        // No specific CanDo checks needed for raw locking, but we follow the pattern for consistency
        if (!CanLockDoor(ent))
            return false;

        LockDoor(ent);
        return true;
    }

    public bool TryUnlockDoor(Entity<PrisonCellDoorComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!CanUnlockDoor(ent))
            return false;

        UnlockDoor(ent);
        return true;
    }

    public bool CanLockDoor(Entity<PrisonCellDoorComponent?> ent, bool quiet = false)
    {
        return HasComp<AccessReaderComponent>(ent);
    }

    public bool CanUnlockDoor(Entity<PrisonCellDoorComponent?> ent, bool quiet = false)
    {
        return HasComp<AccessReaderComponent>(ent);
    }

    public void LockDoor(EntityUid uid)
    {
        if (TryComp<AccessReaderComponent>(uid, out var reader))
        {
            _accessReader.SetActive((uid, reader), true);
        }
    }

    public void UnlockDoor(EntityUid uid)
    {
        if (TryComp<AccessReaderComponent>(uid, out var reader))
        {
            _accessReader.SetActive((uid, reader), false);
        }
    }
}

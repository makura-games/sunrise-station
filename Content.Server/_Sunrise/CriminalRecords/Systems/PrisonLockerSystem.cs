using Content.Shared.Access.Components;
using Content.Server._Sunrise.CriminalRecords.Components;
using Content.Shared._Sunrise.CriminalRecords.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.Lock;
using Content.Shared.Storage.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.CriminalRecords.Systems;

public sealed partial class PrisonLockerSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrisonLockerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PrisonLockerComponent, StorageOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<PrisonLockerComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<PrisonLockerComponent, LockToggledEvent>(OnLockToggled);
    }

    private void OnStartup(Entity<PrisonLockerComponent> ent, ref ComponentStartup args)
    {
        _deviceLink.EnsureSinkPorts(ent, "PrisonLockerLock", "PrisonLockerUnlock");
    }

    public void LockLocker(EntityUid uid, string accessId)
    {
        if (TryComp<AccessReaderComponent>(uid, out var reader))
        {
            _accessReader.SetActive((uid, reader), true);

            if (TryComp<PrisonLockerComponent>(uid, out var locker))
            {
                if (!string.IsNullOrEmpty(locker.AccessId))
                    _accessReader.TryRemoveAccess((uid, reader), new ProtoId<AccessLevelPrototype>(locker.AccessId));

                var accessProto = new ProtoId<AccessLevelPrototype>(accessId);
                _accessReader.TryAddAccess((uid, reader), accessProto);
                locker.AccessId = accessId;
            }
        }
    }

    public void UnlockLocker(EntityUid uid)
    {
        if (TryComp<AccessReaderComponent>(uid, out var reader))
        {
            _accessReader.SetActive((uid, reader), false);

            if (TryComp<PrisonLockerComponent>(uid, out var locker))
            {
                if (!string.IsNullOrEmpty(locker.AccessId))
                    _accessReader.TryRemoveAccess((uid, reader), new ProtoId<AccessLevelPrototype>(locker.AccessId));

                locker.AccessId = string.Empty;
            }
        }
    }

    private void OnSignalReceived(Entity<PrisonLockerComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port == "PrisonLockerLock")
        {
            if (TryComp<AccessReaderComponent>(ent, out var reader))
                _accessReader.SetActive((ent, reader), true);
        }
        else if (args.Port == "PrisonLockerUnlock")
        {
            UnlockLocker(ent.Owner);
        }
    }

    private void OnOpenAttempt(Entity<PrisonLockerComponent> ent, ref StorageOpenAttemptEvent args)
    {
        ent.Comp.LastUser = args.User;
    }

    private void OnLockToggled(Entity<PrisonLockerComponent> ent, ref LockToggledEvent args)
    {
        if (args.Locked || string.IsNullOrEmpty(ent.Comp.AccessId) || args.User == null)
            return;

        var user = args.User.Value;
        var items = _accessReader.FindPotentialAccessItems(user);
        var accessToRemove = new ProtoId<AccessLevelPrototype>(ent.Comp.AccessId);

        foreach (var item in items)
        {
            if (!TryComp<AccessComponent>(item, out var access))
                continue;

            if (access.Tags.Contains(accessToRemove))
            {
                QueueDel(item);
                break;
            }
        }
    }
}

namespace Content.Server.Wires;
using Content.Shared.Doors.Components;

public abstract partial class BaseWireAction : IWireAction
{
    public void WireCutSparks(EntityUid uid)
    {
        if (!IsPowered(uid))
            return;
        if (!EntityManager.TryGetComponent<DoorComponent>(uid, out var door)
            || !door.WireCutSparks
            || !EntityManager.TryGetComponent<TransformComponent>(uid, out var tform))
            return;
        EntityManager.SpawnAttachedTo("EffectMechSparks", tform.Coordinates);
    }
}

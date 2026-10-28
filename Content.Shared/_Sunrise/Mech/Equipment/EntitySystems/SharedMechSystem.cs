using Content.Shared.Mech.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.Mech.EntitySystems;

public abstract partial class SharedMechSystem
{
    /// <summary>
    /// Отменяет попытку выстрела, если стреляет оружие пилота, а не установленное в мехе
    /// </summary>
    private void OnPilotShotAttempt(Entity<MechPilotComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!TryComp<MechComponent>(ent.Comp.Mech, out var mech))
            return;

        if (mech.EquipmentContainer.Contains(args.Used))
            return;

        args.Cancel();
    }
}

using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Weapons.Ranged.Systems
{
    public sealed partial class GunSystem
    {
        // Sunrise-Edit start - restore SetMagState for BatteryWeaponFireModesVisualSystem
        public void SetMagState(EntityUid uid, string? magState, bool force = false, MagazineVisualsComponent? component = null)
        {
            if (!Resolve(uid, ref component, false))
                return;

            if (!force && component.MagState == magState)
                return;

            component.MagState = magState;

            if (TryComp<AppearanceComponent>(uid, out var appearance))
            {
                var appearanceSystem = EntityManager.System<SharedAppearanceSystem>();
                appearanceSystem.QueueUpdate(uid, appearance);
            }
        }
        // Sunrise-Edit end
    }
}

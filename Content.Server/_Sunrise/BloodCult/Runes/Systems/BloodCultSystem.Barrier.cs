using Content.Server._Sunrise.BloodCult.Runes.Comps;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Items;
using Content.Shared.Interaction;
using Content.Shared.Stealth.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Sunrise.BloodCult.Runes.Systems
{
    public partial class BloodCultSystem
    {
        [Dependency] private PhysicsSystem _physicsSystem = default!;

        public void InitializeBarrierSystem()
        {
            SubscribeLocalEvent<CultBarrierComponent, ActivateInWorldEvent>(OnActivateBarrier);
            SubscribeLocalEvent<CultBarrierComponent, InteractUsingEvent>(OnInteract);
        }


        private void OnActivateBarrier(EntityUid uid, CultBarrierComponent component, ActivateInWorldEvent args)
        {
            if (!HasComp<BloodCultistComponent>(args.User))
                return;

            if (component.Activated)
                Deactivate(args.Target);
            else
                Activate(args.Target);
        }

        private void Activate(EntityUid barrier)
        {
            if (!TryComp<CultBarrierComponent>(barrier, out var barrierComponent))
                return;

            if (HasComp<StealthOnMoveComponent>(barrier))
                RemComp<StealthOnMoveComponent>(barrier);

            if (HasComp<StealthComponent>(barrier))
                RemComp<StealthComponent>(barrier);

            _physicsSystem.SetCanCollide(barrier, true);

            barrierComponent.Activated = true;
        }

        private void Deactivate(EntityUid barrier)
        {
            if (!TryComp<CultBarrierComponent>(barrier, out var barrierComponent))
                return;

            EnsureComp<StealthComponent>(barrier);
            EnsureComp<StealthOnMoveComponent>(barrier);

            _physicsSystem.SetCanCollide(barrier, false);

            barrierComponent.Activated = false;
        }

        private void OnInteract(EntityUid uid, CultBarrierComponent component, InteractUsingEvent args)
        {
            var user = args.User;
            var target = args.Target;

            if (!HasComp<BloodCultWeaponComponent>(args.Used))
                return;

            if (!HasComp<BloodCultistComponent>(user))
                return;

            _popupSystem.PopupEntity("Вы уничтожаете барьер", user, user);

            _entityManager.DeleteEntity(target);
        }
    }
}

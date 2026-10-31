using Content.Shared._Sunrise.Abilities;
using Content.Shared.Actions;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Sunrise.Abilities
{
    public sealed partial class PieThrowerSystem : EntitySystem
    {
        [Dependency] private SharedActionsSystem _actions = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private TransformSystem _transformSystem = default!;
        [Dependency] private ThrowingSystem _throwing = default!;
        [Dependency] private SharedAudioSystem _audioSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PieThrowerComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<PieThrowerComponent, PieThrowActionEvent>(OnThrowPie);
        }

        private void OnInit(EntityUid uid, PieThrowerComponent component, ComponentInit args)
        {
            _actions.AddAction(uid, component.ActionPieThrow);
        }

        private void OnThrowPie(EntityUid uid, PieThrowerComponent component, PieThrowActionEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            var hugger = Spawn(component.PieProtoId, Transform(uid).Coordinates);
            var xform = Transform(uid);
            var mapCoords = args.Target.ToMap(_entityManager, _transformSystem);
            var direction = mapCoords.Position - xform.MapPosition.Position;

            _throwing.TryThrow(hugger, direction, 7F, uid, 10F);
            if (component.SoundThrowPie != null)
            {
                _audioSystem.PlayPvs(component.SoundThrowPie, uid, component.SoundThrowPie.Params);
            }
        }
    }
}

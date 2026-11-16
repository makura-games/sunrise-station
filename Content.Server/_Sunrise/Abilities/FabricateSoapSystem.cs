using Content.Shared._Sunrise.Abilities;
using Content.Shared.Actions;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Abilities
{
    public sealed partial class FabricateSoapSystem : EntitySystem
    {
        [Dependency] private SharedActionsSystem _actions = default!;
        [Dependency] private IRobustRandom _random = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<FabricateSoapComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<FabricateSoapComponent, FabricateSoapActionEvent>(OnCookie);
        }

        private void OnInit(EntityUid uid, FabricateSoapComponent component, ComponentInit args)
        {
            _actions.AddAction(uid, component.ActionFabricateSoap);
        }

        private void OnCookie(EntityUid uid, FabricateSoapComponent component, FabricateSoapActionEvent args)
        {
            Spawn(_random.Pick(component.SoapList), Transform(args.Performer).Coordinates);
            args.Handled = true;
        }
    }
}

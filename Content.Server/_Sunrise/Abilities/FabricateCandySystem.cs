using Content.Shared._Sunrise.Abilities;
using Content.Shared.Actions;

namespace Content.Server._Sunrise.Abilities
{
    public sealed partial class FabricateCandySystem : EntitySystem
    {
        [Dependency] private SharedActionsSystem _actions = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<FabricateCandyComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<FabricateCandyComponent, FabricateLollipopActionEvent>(OnLollipop);
            SubscribeLocalEvent<FabricateCandyComponent, FabricateGumballActionEvent>(OnGumball);
        }

        private void OnInit(EntityUid uid, FabricateCandyComponent component, ComponentInit args)
        {
            _actions.AddAction(uid, component.ActionFabricateLollipop);
            _actions.AddAction(uid, component.ActionFabricateGumball);
        }

        private void OnLollipop(EntityUid uid, FabricateCandyComponent component, FabricateLollipopActionEvent args)
        {
            Spawn(component.FoodLollipopId, Transform(args.Performer).Coordinates);
            args.Handled = true;
        }

        private void OnGumball(EntityUid uid, FabricateCandyComponent component, FabricateGumballActionEvent args)
        {
            Spawn(component.FoodGumballId, Transform(args.Performer).Coordinates);
            args.Handled = true;
        }
    }
}

using Content.Shared._Sunrise.Abilities;
using Content.Shared.Actions;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Abilities
{
    public sealed partial class FabricateCookieSystem : EntitySystem
    {
        [Dependency] private SharedActionsSystem _actions = default!;
        [Dependency] private IRobustRandom _random = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<FabricateCookieComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<FabricateCookieComponent, FabricateCookieActionEvent>(OnCookie);
        }

        private void OnInit(EntityUid uid, FabricateCookieComponent component, ComponentInit args)
        {
            _actions.AddAction(uid, component.ActionFabricateCookie);
        }

        private void OnCookie(EntityUid uid, FabricateCookieComponent component, FabricateCookieActionEvent args)
        {
            Spawn(_random.Pick(component.CookieList), Transform(args.Performer).Coordinates);
            args.Handled = true;
        }
    }
}

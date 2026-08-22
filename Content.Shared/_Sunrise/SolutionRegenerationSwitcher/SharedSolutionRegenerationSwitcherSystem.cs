using Content.Shared.Chemistry.Reagent;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.SolutionRegenerationSwitcher
{
    public abstract partial class SharedSolutionRegenerationSwitcherSystem : EntitySystem
    {
        [Dependency] protected IPrototypeManager PrototypeManager = null!;

        private ISawmill _sawmill = null!;

        public override void Initialize()
        {
            base.Initialize();

            _sawmill = Logger.GetSawmill("chemistry");

            SubscribeLocalEvent<SolutionRegenerationSwitcherComponent, GetVerbsEvent<Verb>>(AddSwitchVerb);
            SubscribeLocalEvent<SolutionRegenerationSwitcherComponent, GetVerbsEvent<AlternativeVerb>>(
                AddAlternateSwitchVerb);
        }

        private void AddSwitchVerb(EntityUid uid,
            SolutionRegenerationSwitcherComponent component,
            GetVerbsEvent<Verb> args)
        {
            if (!args.CanInteract || !args.CanAccess)
                return;

            for (var i = 0; i < component.Options.Count; i++)
            {
                var componentOption = component.Options[i];
                if (!PrototypeManager.TryIndex(componentOption.Reagent.Prototype, out ReagentPrototype? proto))
                {
                    _sawmill.Error(
                        $"Can't get reagent prototype {componentOption.Reagent.Prototype} for {ToPrettyString(uid)}");
                    return;
                }

                var index = i;
                Verb reagent = new()
                {
                    Text = proto.LocalizedName,
                    Icon = i == component.CurrentIndex
                        ? new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/spill.svg.192dpi.png"))
                        : null,
                    Act = () =>
                    {
                        component.CurrentIndex = index;
                        SwitchReagent(uid, componentOption, component, args.User);
                    },
                    Priority = 20,
                    Message = Loc.GetString("solution-regeneration-switcher-switch-verb-text"),
                };
                args.Verbs.Add(reagent);
            }
        }

        private void AddAlternateSwitchVerb(EntityUid uid,
            SolutionRegenerationSwitcherComponent component,
            GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess)
                return;

            if (component.Options.Count <= 1)
                return;

            AlternativeVerb verb = new()
            {
                Act = () =>
                {
                    SwitchToNextReagent(uid, component, args.User);
                },
                Text = Loc.GetString("solution-regeneration-switcher-switch-reagent-alt"),
                Priority = 2,
            };
            args.Verbs.Add(verb);
        }

        protected abstract void SwitchToNextReagent(EntityUid uid,
            SolutionRegenerationSwitcherComponent component,
            EntityUid user);


        protected abstract void SwitchReagent(EntityUid uid,
            ReagentQuantity reagent,
            SolutionRegenerationSwitcherComponent component,
            EntityUid user);
    }
}

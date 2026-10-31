using Content.Shared._Starlight.Medical.Limbs;
using Content.Shared.Actions.Components;
using Content.Shared.Body;


namespace Content.Server._Starlight.Medical.Limbs;

public sealed partial class CyberLimbSystem : EntitySystem
{
    public void InitializeToggleable()
    {
        SubscribeLocalEvent<BodyComponent, LimbAttachedEvent<IWithAction>>(IWithActionAttached);
        SubscribeLocalEvent<BodyComponent, LimbRemovedEvent<IWithAction>>(IWithActionRemoved);
    }

    private void IWithActionRemoved(Entity<BodyComponent> ent, ref LimbRemovedEvent<IWithAction> args)
        => _actions.RemoveProvidedActions(ent.Owner, args.Limb);

    private void IWithActionAttached(Entity<BodyComponent> ent, ref LimbAttachedEvent<IWithAction> args)
        => _actions.GrantContainedActions(_slEnt.Entity<ActionsComponent>(ent), _slEnt.Entity<ActionsContainerComponent>(args.Limb));
}

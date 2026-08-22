using System.Linq;
using Content.Shared._Sunrise.Antags.Abductor;
using Content.Shared.Actions.Components;

namespace Content.Shared.Actions;

public abstract partial class SharedActionsSystem
{
    [Dependency] private EntityQuery<TargetActionComponent> _sunriseTargetActionQuery = default!;

    private bool CanSunriseBypassEntityTargetValidation(EntityUid target)
    {
        return HasComp<AbductorComponent>(target);
    }

    private static bool CanSunriseIgnoreContainer(TargetActionComponent component)
    {
        return component.IgnoreContainer;
    }

    private bool CanSunriseBypassWorldTargetValidation(EntityUid user)
    {
        return HasComp<AbductorAgentComponent>(user) || HasComp<AbductorScientistComponent>(user);
    }

    public EntityUid[] HideActions(EntityUid performer, ActionsComponent? component = null)
    {
        if (!Resolve(performer, ref component, false))
            return [];

        var actions = component.Actions.ToArray();
        component.Actions.Clear();
        Dirty(performer, component);
        return actions;
    }

    public void UnHideActions(EntityUid performer, EntityUid[] actions, ActionsComponent? component = null)
    {
        if (!Resolve(performer, ref component, false))
            return;

        foreach (var action in actions)
        {
            component.Actions.Add(action);
        }

        Dirty(performer, component);
    }

    public void SetItemIconStyle(Entity<ActionComponent?> ent, ItemActionIconStyle itemIconStyle)
    {
        if (!_actionQuery.Resolve(ent, ref ent.Comp) || ent.Comp.ItemIconStyle == itemIconStyle)
            return;

        ent.Comp.ItemIconStyle = itemIconStyle;
        DirtyField(ent, ent.Comp, nameof(ActionComponent.ItemIconStyle));
    }

    public void SetPriority(Entity<ActionComponent?> ent, int priority)
    {
        if (!_actionQuery.Resolve(ent, ref ent.Comp) || ent.Comp.Priority == priority)
            return;

        ent.Comp.Priority = priority;
        DirtyField(ent, ent.Comp, nameof(ActionComponent.Priority));
    }

    public void SetCheckCanInteract(Entity<ActionComponent?> ent, bool value)
    {
        if (!_actionQuery.Resolve(ent, ref ent.Comp) || ent.Comp.CheckCanInteract == value)
            return;

        ent.Comp.CheckCanInteract = value;
        DirtyField(ent, ent.Comp, nameof(ActionComponent.CheckCanInteract));
    }

    public void SetCheckCanAccess(Entity<TargetActionComponent?> ent, bool value)
    {
        if (!_sunriseTargetActionQuery.Resolve(ent, ref ent.Comp) || ent.Comp.CheckCanAccess == value)
            return;

        ent.Comp.CheckCanAccess = value;
        DirtyField(ent, ent.Comp, nameof(TargetActionComponent.CheckCanAccess));
    }

    public void SetIgnoreContainer(Entity<TargetActionComponent?> ent, bool value)
    {
        if (!_sunriseTargetActionQuery.Resolve(ent, ref ent.Comp) || ent.Comp.IgnoreContainer == value)
            return;

        ent.Comp.IgnoreContainer = value;
        DirtyField(ent, ent.Comp, nameof(TargetActionComponent.IgnoreContainer));
    }
}

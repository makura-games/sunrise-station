using Content.Shared._Sunrise.ThermalVision;
using Content.Shared.Actions;
using Content.Shared.Toggleable;

namespace Content.Server._Sunrise.ThermalVision;

public sealed partial class ToggleableThermalVisionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableThermalVisionComponent, ComponentInit>(OnVisionInit);
        SubscribeLocalEvent<ToggleableThermalVisionComponent, ComponentShutdown>(OnVisionShutdown);
        SubscribeLocalEvent<ToggleableThermalVisionComponent, ToggleActionEvent>(OnToggleThermalVision);
    }

    private void OnVisionInit(Entity<ToggleableThermalVisionComponent> ent, ref ComponentInit args)
    {
        _actionsSystem.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnVisionShutdown(Entity<ToggleableThermalVisionComponent> ent, ref ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(ent.Comp.ActionEntity);
        RemComp<ThermalVisionComponent>(ent);
    }

    private void OnToggleThermalVision(Entity<ToggleableThermalVisionComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.Active = !ent.Comp.Active;

        if (ent.Comp.Active)
            EnsureComp<ThermalVisionComponent>(ent);
        else
            RemComp<ThermalVisionComponent>(ent);

        args.Handled = true;
        Dirty(ent, ent.Comp);
    }
}

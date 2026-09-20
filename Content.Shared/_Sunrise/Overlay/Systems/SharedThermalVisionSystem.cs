using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Content.Shared._Sunrise.Overlay.Components;
using Content.Shared._Sunrise.Overlay.Events;

namespace Content.Shared._Sunrise.Overlay.Systems;

public abstract partial class SharedThermalVisionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;

    protected virtual bool IsPredict() => false;
    public EntProtoId Action = "ActionToggleThermal";
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StarlightThermalVisionComponent, MapInitEvent>(OnVisionInit);
        SubscribeLocalEvent<StarlightThermalVisionComponent, ComponentShutdown>(OnVisionShutdown);
        SubscribeLocalEvent<StarlightThermalVisionComponent, ToggleThermalVisionEvent>(OnToggleThermalVision);
    }

    private void OnVisionInit(Entity<StarlightThermalVisionComponent> ent, ref MapInitEvent args)
    {
        _actionsSystem.AddAction(ent.Owner, ref ent.Comp.ActionEntity, Action);
    }

    private void OnVisionShutdown(Entity<StarlightThermalVisionComponent> ent, ref ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(ent.Comp.ActionEntity);
        //принудительное выключение
        ToggleOff(ent);
    }

    private void OnToggleThermalVision(Entity<StarlightThermalVisionComponent> ent, ref ToggleThermalVisionEvent args)
    {
        if(args.Handled || IsPredict()) return;
        args.Handled = true;

        ent.Comp.Active = !ent.Comp.Active;

        if(ent.Comp.Active)
            ToggleOn(ent);
        else
            ToggleOff(ent);
    }
    protected virtual void ToggleOn(Entity<StarlightThermalVisionComponent> ent)
    {

    }
    protected virtual void ToggleOff(Entity<StarlightThermalVisionComponent> ent)
    {

    }
}


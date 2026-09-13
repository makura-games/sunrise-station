using Content.Shared._Sunrise.Boss.Components;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Boss.Systems;

/// <inheritdoc/>
public abstract partial class SharedHellSpawnTentacleSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    // Статические значения

    public static EntProtoId GrabRightEntityId = "HellspawnGrabRight";
    public static EntProtoId GrabLeftEntityId = "HellspawnGrabLeft";

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HellSpawnTentacleComponent, ComponentInit>(OnTentacleInit);
    }

    private void OnTentacleInit(EntityUid uid, HellSpawnTentacleComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.LeftGrabActionEntity, component.LeftGrabAction);
        _actions.AddAction(uid, ref component.RightGrabActionEntity, component.RightGrabAction);
    }
}

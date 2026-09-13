using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared.Interaction;

namespace Content.Server._Sunrise.BloodCult.Structures;

public sealed partial class RunicGirderSystem : EntitySystem
{
    [Dependency] private EntityManager _entMan = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RunicGirderComponent, AfterInteractUsingEvent>(OnInteract);
    }

    private void OnInteract(EntityUid uid, RunicGirderComponent component, AfterInteractUsingEvent args)
    {
        if (!HasComp<BloodCultistComponent>(args.User))
            return;
        if (MetaData(args.Used).EntityPrototype?.ID != component.UsedItemID)
            return;
        if (args.Target == null)
            return;

        var pos = Transform(args.Target.Value).Coordinates;
        _entMan.DeleteEntity(args.Target.Value);
        _entMan.SpawnEntity(component.DropItemID, pos);
    }
}

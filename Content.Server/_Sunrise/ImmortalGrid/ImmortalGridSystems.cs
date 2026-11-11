using Content.Server.Station.Events;
using Content.Shared._Sunrise.BlockedTool;
using Content.Shared.Damage.Components;
using Content.Shared.Tiles;

namespace Content.Server._Sunrise.ImmortalGrid;

public sealed partial class ImmortalGridSystems : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImmortalGridComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StationImmortalComponent, StationPostInitEvent>(OnPostInit);
    }

    private void OnPostInit(EntityUid uid, StationImmortalComponent component, ref StationPostInitEvent args)
    {
        foreach (var gridUid in args.Station.Comp.Grids)
        {
            AddComp<ImmortalGridComponent>(gridUid);
        }
    }

    private void OnStartup(EntityUid uid, ImmortalGridComponent component, ref ComponentStartup args)
    {
        EnsureComp<ProtectedGridComponent>(uid);

        var entities = new HashSet<Entity<TransformComponent>>();
        _lookup.GetChildEntities(uid, entities);

        foreach (var entityUid in entities)
        {
            EnsureComp<GodmodeComponent>(entityUid);
            EnsureComp<BlockedToolComponent>(entityUid);
        }
    }
}

using Content.Server.Station.Systems;
using Content.Shared._Sunrise.BlockedTool;
using Content.Shared.Damage.Components;
using Robust.Shared.Map;

namespace Content.Server._Sunrise.TradeStation;

public sealed class SunriseTradeStationGridSystem : EntitySystem
{
    [Dependency] private StationSystem _station = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SunriseTradeStationGridComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SunriseTradeStationGridComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<SunriseTradeStationGridComponent> ent, ref ComponentStartup args)
    {
        if (_station.GetOwningStation(ent) != null)
            return;

        var station = Spawn("SunriseTradeStationAccount", MapCoordinates.Nullspace);
        _station.AddGridToStation(station, ent);
        ent.Comp.Station = station;

        var children = new HashSet<Entity<TransformComponent>>();
        _lookup.GetChildEntities(ent.Owner, children);
        foreach (var child in children)
        {
            if (!child.Comp.Anchored || HasComp<SunriseTradeStationComputerComponent>(child))
                continue;

            EnsureComp<GodmodeComponent>(child);
            EnsureComp<BlockedToolComponent>(child);
        }
    }

    private void OnShutdown(Entity<SunriseTradeStationGridComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Station is { } station)
            QueueDel(station);
    }
}

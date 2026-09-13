using System.Numerics;
using Content.Server.Shuttles.Systems;
using Content.Server._Sunrise.TransitHub;
using Content.Server._Sunrise.ImmortalGrid;
using Content.Shared._Sunrise.AlwaysPoweredMap;
using Content.Shared._Sunrise.UnbuildableGrid;
using Robust.Shared.Utility;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Content.Server.Parallax;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Light.Components;
using Content.Shared.Tiles;
using Content.Server.Shuttles.Components;
using Content.Shared.Salvage;
using Robust.Shared.Random;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem
{
    private void OnCentcommShutdown(EntityUid uid, StationTransitHubComponent component, ComponentShutdown args) // Sunrise-Edit
    {
        ClearTransitHub(component);
    }

    private void ClearTransitHub(StationTransitHubComponent component) // Sunrise-Edit
    {
        QueueDel(component.Entity);
        QueueDel(component.MapEntity);
        component.Entity = null;
        component.MapEntity = null;
    }

    private void OnTransitHubInit(EntityUid uid, StationTransitHubComponent component, MapInitEvent args) // Sunrise-Edit
    {
        // This is handled on map-init, so that centcomm has finished initializing by the time the StationPostInitEvent
        // gets raised
        if (!_emergencyShuttleEnabled)
            return;

        // Post mapinit? fancy
        if (TryComp<TransformComponent>(component.Entity, out var xform)) // Sunrise-Edit
        {
            component.MapEntity = xform.MapUid;
            return;
        }

        AddTransitHub(uid, component); // Sunrise-Edit
    }

    private void AddTransitHub(EntityUid station, StationTransitHubComponent component) // Sunrise-Edit
    {
        DebugTools.Assert(LifeStage(station) >= EntityLifeStage.MapInitialized);
        if (component.MapEntity != null || component.Entity != null)
        {
            Log.Warning("Attempted to re-add an existing centcomm map.");
            return;
        }

        // Check for existing centcomms and just point to that
        var query = AllEntityQuery<StationTransitHubComponent>(); // Sunrise-Edit
        while (query.MoveNext(out var otherComp))
        {
            if (otherComp == component)
                continue;

            if (!Exists(otherComp.MapEntity) || !Exists(otherComp.Entity))
            {
                Log.Error($"Discovered invalid centcomm component?");
                ClearTransitHub(otherComp);
                continue;
            }

            component.MapEntity = otherComp.MapEntity;
            component.Entity = otherComp.Entity;
            return;
        }

        if (string.IsNullOrEmpty(component.Map.ToString()))
        {
            Log.Warning("No CentComm map found, skipping setup.");
            return;
        }

        // Sunrise-start
        var mapUid = _mapSystem.CreateMap(out var mapId, runMapInit: false);

        if (!_loader.TryLoadGrid(mapId, component.Map, out var uid))
        {
            Log.Error($"Failed to set up transit hub map!");
            QueueDel(mapUid);
            return;
        }

        EnsureComp<LightCycleComponent>(mapUid);

        Log.Info($"Created transit hub grid {ToPrettyString(uid)} on map {ToPrettyString(mapUid)} for station {ToPrettyString(station)}");

        EnsureComp<ProtectedGridComponent>(uid.Value.Owner);
        EnsureComp<ArrivalsSourceComponent>(uid.Value.Owner); // Sunrise-edit
        EnsureComp<UnbuildableGridComponent>(uid.Value.Owner); // Sunrise-edit
        EnsureComp<ImmortalGridComponent>(uid.Value.Owner); // Sunrise-edit

        var template = _random.Pick(component.Biomes);
        var biome = _prototypeManager.Index<BiomeTemplatePrototype>(template);
        _biomes.EnsurePlanet(mapUid, biome);

        component.MapEntity = mapUid;
        component.Entity = uid;

        // Sunrise-Start
        var restricted = new RestrictedRangeComponent
        {
            Origin = new Vector2(0, 0),
            Range = 160,
        };
        AddComp(mapUid, restricted);
        // Sunrise-End

        _mapManager.DoMapInitialize(mapId);
        // Sunrise-end
    }

    // Sunrise-start
    public HashSet<EntityUid> GetTransitHubMaps()
    {
        var query = AllEntityQuery<StationTransitHubComponent>();
        var maps = new HashSet<EntityUid>(Count<StationTransitHubComponent>());

        while (query.MoveNext(out var comp))
        {
            if (Exists(comp.MapEntity))
                maps.Add(comp.MapEntity.Value);
        }

        return maps;
    }
    // Sunrise-end

    private void EnableTransitHubFtlDestination()
    {
        var query = AllEntityQuery<StationTransitHubComponent>();

        while (query.MoveNext(out var component))
        {
            if (!TryComp(component.MapEntity, out MapComponent? map))
                continue;

            if (_shuttle.TryAddFTLDestination(map.MapId, true, out var destination))
                _shuttle.SetFTLWhitelist((component.MapEntity.Value, destination), null);
        }
    }
}

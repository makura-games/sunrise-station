using Content.Server.Botany.Components;
using Content.Server.Damage.Systems;
using Content.Server.Popups;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Server._Sunrise.Soil;

public sealed class SoilSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<PlantHolderComponent> _plantHolderQuery;

    public override void Initialize()
    {
        base.Initialize();

        _plantHolderQuery = GetEntityQuery<PlantHolderComponent>();

        SubscribeLocalEvent<SoilComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<SoilComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryPlantSoil(ent, args.User);
    }

    public bool TryPlantSoil(Entity<SoilComponent> ent, EntityUid user)
    {
        if (!CanPlantSoil(ent, user, out var coords))
            return false;

        DoPlantSoil(ent, user, coords);
        return true;
    }

    public bool CanPlantSoil(Entity<SoilComponent> ent, EntityUid user, out EntityCoordinates coords)
    {
        var userCoords = _transform.GetMapCoordinates(user);
        if (!_mapManager.TryFindGridAt(userCoords, out var gridUid, out var grid))
        {
            coords = EntityCoordinates.Invalid;
            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupStringFailed), ent, user, PopupType.SmallCaution);
            return false;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, userCoords);
        coords = _map.ToCenterCoordinates(gridUid, tile, grid);

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var entity))
        {
            if (!_plantHolderQuery.HasComp(entity))
                continue;

            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupStringFailed), ent, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private void DoPlantSoil(Entity<SoilComponent> ent, EntityUid user, EntityCoordinates coords)
    {
        Spawn(ent.Comp.SpawnPrototype, coords);

        var targetMeta = MetaData(ent);
        var userMeta = MetaData(user);

        _popup.PopupEntity(
            Loc.GetString(ent.Comp.PopupStringSuccess, ("user", userMeta.EntityName), ("name", targetMeta.EntityName)),
            user,
            PopupType.LargeGreen);
        _stamina.TryTakeStamina(user, ent.Comp.StaminaDamage);

        QueueDel(ent);
    }
}

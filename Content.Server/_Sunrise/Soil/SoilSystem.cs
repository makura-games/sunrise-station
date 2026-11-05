using System.Collections.Generic;
using Content.Shared.Botany.Components;
using Content.Server.Damage.Systems;
using Content.Server.Popups;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._Sunrise.Soil;

/// <summary>
/// Система для мешка с землей.
/// </summary>
public sealed partial class SoilSystem : EntitySystem
{
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoilComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<SoilComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var userXform = Transform(args.User);
        var grid = _transform.GetGrid((args.User, userXform));

        if (grid == null ||
            !TryComp<MapGridComponent>(grid.Value, out var gridComp))
        {
            ShowPlantFailure(ent, args.User);
            return;
        }

        var userCoords = _transform.GetMapCoordinates(args.User);

        var tile = _map.TileIndicesFor(
            grid.Value,
            gridComp,
            userCoords);

        var spawnCoords = _map.ToCenterCoordinates(
            grid.Value,
            tile,
            gridComp);

        var entitiesOnTile = new HashSet<EntityUid>();

        _lookup.GetLocalEntitiesIntersecting(
            grid.Value,
            tile,
            entitiesOnTile,
            flags: LookupFlags.Uncontained,
            gridComp: gridComp);

        foreach (var entity in entitiesOnTile)
        {
            if (!HasComp<PlantTrayComponent>(entity))
                continue;

            ShowPlantFailure(ent, args.User);
            return;
        }

        args.Handled = true;

        _entity.SpawnEntity(ent.Comp.SpawnPrototype, spawnCoords);

        var targetMeta = MetaData(ent);
        var userMeta = MetaData(args.User);

        _popup.PopupEntity(
            Loc.GetString(
                ent.Comp.PopupStringSuccess,
                ("user", userMeta.EntityName),
                ("name", targetMeta.EntityName)),
            args.User,
            PopupType.LargeGreen);

        _stamina.TryTakeStamina(args.User, ent.Comp.StaminaDamage);

        _entity.QueueDeleteEntity(ent.Owner);
    }

    private void ShowPlantFailure(Entity<SoilComponent> ent, EntityUid user)
    {
        _popup.PopupEntity(
            Loc.GetString(ent.Comp.PopupStringFailed),
            ent,
            user,
            PopupType.SmallCaution);
    }
}
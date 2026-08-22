using Robust.Server.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network.Messages;
using Robust.Shared.Placement;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Mapping;

/// <summary>
/// Applies content-defined replacement rules before mapper placement falls back to engine behavior.
/// </summary>
public sealed partial class MappingReplacementSystem : EntitySystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery = default!;
    [Dependency] private EntityQuery<MappingReplacementComponent> _replacementQuery = default!;

    private readonly List<EntityUid> _anchoredEntities = [];

    /// <summary>
    /// Removes entities approved by content replacement rules and then allows placement to continue.
    /// </summary>
    public bool TryHandlePlacementReplacement(MsgPlacement msg)
    {
        if (!CanHandlePlacementReplacement(msg, out var replacement, out var coordinates, out var grid))
            return true;

        DoHandlePlacementReplacement(msg, replacement, coordinates, grid);
        return true;
    }

    private bool CanHandlePlacementReplacement(
        MsgPlacement msg,
        out MappingReplacementComponent replacement,
        out EntityCoordinates coordinates,
        out Entity<MapGridComponent> grid)
    {
        replacement = default!;
        coordinates = default;
        grid = default;

        if (msg.PlaceType != PlacementManagerMessage.RequestPlacement)
            return false;

        if (msg.IsTile)
            return false;

        if (!msg.Replacement)
            return false;

        if (string.IsNullOrEmpty(msg.EntityTemplateName))
            return false;

        coordinates = GetCoordinates(msg.NetCoordinates);
        if (!coordinates.IsValid(EntityManager))
            return false;

        if (!TryGetReplacementComponent(msg.EntityTemplateName, out replacement))
            return false;

        var gridUid = _transform.GetGrid(coordinates);
        if (gridUid is not { } gridOwner || !_mapGridQuery.TryComp(gridOwner, out var gridComp))
            return false;

        grid = (gridOwner, gridComp);
        return true;
    }

    private void DoHandlePlacementReplacement(
        MsgPlacement msg,
        MappingReplacementComponent replacement,
        EntityCoordinates coordinates,
        Entity<MapGridComponent> grid)
    {
        var key = GetReplacementKey(msg.EntityTemplateName, replacement);
        var indices = _map.TileIndicesFor(grid.Owner, grid.Comp, coordinates);

        _anchoredEntities.Clear();
        _map.GetAnchoredEntities(grid, indices, _anchoredEntities);

        foreach (var otherEntity in _anchoredEntities)
        {
            if (!_replacementQuery.TryComp(otherEntity, out var otherReplacement))
                continue;

            if (GetReplacementKey(otherEntity, otherReplacement) != key)
                continue;

            if ((replacement.RequireSameRotation || otherReplacement.RequireSameRotation) &&
                Transform(otherEntity).LocalRotation.GetDir() != msg.DirRcv)
            {
                continue;
            }

            var eraseEvent = new PlacementEntityEvent(otherEntity, coordinates, PlacementEventAction.Erase, msg.MsgChannel.UserId);
            RaiseLocalEvent(eraseEvent);
            Del(otherEntity);
        }
    }

    private bool TryGetReplacementComponent(string prototypeId, out MappingReplacementComponent replacement)
    {
        replacement = default!;

        var prototype = _prototype.Index(prototypeId);
        if (!prototype.Components.TryGetValue(_factory.GetComponentName<MappingReplacementComponent>(), out var compRegistry))
            return false;

        replacement = (MappingReplacementComponent) compRegistry.Component;
        return true;
    }

    private string GetReplacementKey(string prototypeId, MappingReplacementComponent component)
    {
        if (component.UsePrototypeId || string.IsNullOrEmpty(component.Key))
            return prototypeId;

        return component.Key;
    }

    private string GetReplacementKey(EntityUid uid, MappingReplacementComponent component)
    {
        if (!component.UsePrototypeId && !string.IsNullOrEmpty(component.Key))
            return component.Key;

        return Prototype(uid)?.ID ?? component.Key;
    }
}

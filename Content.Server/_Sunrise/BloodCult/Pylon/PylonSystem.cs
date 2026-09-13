using System.Linq;
using System.Numerics;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Pylon;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.BloodCult.Pylon;

public sealed partial class PylonSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BloodstreamSystem _blood = default!;
    [Dependency] private DamageableSystem _damageSystem = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private MapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharedPylonComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<SharedPylonComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SharedPylonComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
    }

    private void OnAnchorStateChanged(EntityUid uid, SharedPylonComponent component, AnchorStateChangedEvent args)
    {
        component.Activated = args.Anchored;

        UpdateAppearance(uid, component);
    }

    private void OnInit(EntityUid uid, SharedPylonComponent component, ComponentInit args)
    {
        UpdateAppearance(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var pylonsQuery = EntityQueryEnumerator<SharedPylonComponent>();

        while (pylonsQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.NextTileConvert == TimeSpan.Zero)
                comp.NextTileConvert = _timing.CurTime + TimeSpan.FromSeconds(comp.TileConvertCooldown);

            if (comp.NextHealTime == TimeSpan.Zero)
                comp.NextHealTime = _timing.CurTime + TimeSpan.FromSeconds(comp.HealingAuraCooldown);

            if (_timing.CurTime >= comp.NextHealTime)
            {
                comp.NextHealTime = _timing.CurTime + TimeSpan.FromSeconds(comp.HealingAuraCooldown);

                if (comp.Activated)
                    HealPlayersInRange(uid, comp);
            }

            if (_timing.CurTime >= comp.NextTileConvert)
            {
                comp.NextTileConvert = _timing.CurTime + TimeSpan.FromSeconds(comp.TileConvertCooldown);

                if (comp.Activated)
                    ConvertNearbyTiles(comp);
            }
        }
    }

    private void ConvertNearbyTiles(SharedPylonComponent comp)
    {
        var tilesConverted = 0;
        var random = new Random().Next(1, 3);

        var uid = comp.Owner;
        var gridUid = Transform(uid).GridUid;
        var pylonPos = Transform(uid).Coordinates;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var radius = comp.TileConvertRange;
        var tilesRefs = _map.GetLocalTilesIntersecting(gridUid.Value, grid, new Box2(pylonPos.Position + new Vector2(-radius, -radius),
            pylonPos.Position + new Vector2(radius, radius)));
        var tiles = ShuffleTiles(tilesRefs);

        if (comp.ConvertEverything)
            ConvertEverything(comp, tiles);

        var cultTileDef = (ContentTileDefinition)_tileDefinition[$"{comp.TileId}"];
        var cultTile = new Tile(cultTileDef.TileId);

        foreach (var tile in tiles)
        {
            if (tilesConverted >= random)
                return;

            var tilePos = _turf.GetTileCenter(tile);

            if (pylonPos.InRange(EntityManager, tilePos, comp.TileConvertRange))
            {
                if (tile.Tile.TypeId == cultTile.TypeId)
                    continue;

                _tile.ReplaceTile(tile, cultTileDef);
                _entMan.SpawnEntity(comp.TileConvertEffect, tilePos);
                _audio.PlayPvs(comp.ConvertTileSound, tilePos, AudioParams.Default.WithVolume(-5));

                tilesConverted++;
            }
        }
    }

    private void ConvertEverything(SharedPylonComponent comp, IEnumerable<TileRef> tiles)
    {
        foreach (var tile in tiles)
        {
            if (!_turf.IsTileBlocked(tile, CollisionGroup.WallLayer)
                || !_turf.IsTileBlocked(tile, CollisionGroup.AirlockLayer))
                continue;

            var posss = _turf.GetTileCenter(tile);

            foreach (var entity in _lookup.GetEntitiesIntersecting(posss))
            {
                if (TryComp<TagComponent>(entity, out var tag)
                    && tag.Tags.Contains("Wall")
                    && MetaData(entity).EntityPrototype?.ID != comp.WallId)
                {
                    _entMan.SpawnEntity(comp.WallId, Transform(entity).Coordinates);
                    _entMan.SpawnEntity(comp.WallConvertEffect, Transform(entity).Coordinates);
                    _entMan.DeleteEntity(entity);
                    _audio.PlayPvs(comp.ConvertTileSound, posss, AudioParams.Default.WithVolume(-10));
                    return;
                }

                if (HasComp<AirlockComponent>(entity) && MetaData(entity).EntityPrototype?.ID != comp.AirlockId)
                {
                    _entMan.SpawnEntity(comp.AirlockId, Transform(entity).Coordinates);
                    _entMan.SpawnEntity(comp.AirlockConvertEffect, Transform(entity).Coordinates);
                    _entMan.DeleteEntity(entity);
                    _audio.PlayPvs(comp.ConvertTileSound, posss, AudioParams.Default.WithVolume(-10));
                    return;
                }
            }
        }
    }

    private void HealPlayersInRange(EntityUid pylon, SharedPylonComponent comp)
    {
        // SUNRISE-TODO: Че за параша, нахуя итерироваться по всем сессиям
        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            if (!HasComp<BloodCultistComponent>(playerEntity) && !HasComp<ConstructComponent>(playerEntity))
                continue;

            if (_mobStateSystem.IsDead(playerEntity))
                continue;

            var playerDamageComp = TryComp<DamageableComponent>(playerEntity, out var damageComp)
                ? damageComp
                : null;

            if (playerDamageComp == null ||
                _damageSystem.GetTotalDamage((playerEntity, playerDamageComp)) == 0)
                continue;

            var uid = comp.Owner;
            var pylonXForm = Transform(uid);
            var playerXForm = Transform(playerEntity);

            if (pylonXForm.Coordinates.InRange(EntityManager, playerXForm.Coordinates, comp.HealingAuraRange))
            {
                var damage = comp.HealingAuraDamage;
                _damageSystem.TryChangeDamage(playerEntity, damage, true, origin: pylon);

                if (!TryComp<BloodstreamComponent>(playerEntity, out var bloodstream))
                    continue;

                if (bloodstream.BleedAmount > 1)
                {
                    _blood.TryModifyBleedAmount((playerEntity, bloodstream), -comp.BleedReductionAmount);
                }

                if (_blood.GetBloodLevel((playerEntity, bloodstream)) < bloodstream.BloodReferenceSolution.MaxVolume)
                {
                    _blood.TryModifyBloodLevel((playerEntity, bloodstream), comp.BloodRefreshAmount);
                }
            }
        }
    }

    private void OnInteract(EntityUid uid, SharedPylonComponent comp, InteractHandEvent args)
    {
        var user = args.User;
        var pylon = args.Target;

        if (!TryComp(uid, out TransformComponent? transformComponent) || !transformComponent.Anchored)
        {
            return;
        }

        if (HasComp<BloodCultistComponent>(user))
        {
            comp.Activated = !comp.Activated;

            UpdateAppearance(uid, comp);

            if (!TryComp<PointLightComponent>(uid, out var light))
                return;

#pragma warning disable RA0002
            light.Enabled = comp.Activated;
            Dirty(uid, light);
#pragma warning restore RA0002

            var toggleMsg = Loc.GetString(comp.Activated ? "pylon-toggle-on" : "pylon-toggle-off");
            _popupSystem.PopupEntity(toggleMsg, uid);
            return;
        }

        var damage = comp.BurnDamageOnInteract;
        var burnMsg = Loc.GetString("powered-light-component-burn-hand");

        _audio.PlayEntity(comp.BurnHandSound, Filter.Pvs(pylon), pylon, true);
        _popupSystem.PopupEntity(burnMsg, pylon, user);
        _damageSystem.TryChangeDamage(user, damage, true);
    }

    private IEnumerable<TileRef> ShuffleTiles(IEnumerable<TileRef> collection)
    {
        var random = new Random();
        var shuffledList = collection.ToList();

        var n = shuffledList.Count;
        while (n > 1)
        {
            n--;
            var k = random.Next(n + 1);
            (shuffledList[k], shuffledList[n]) = (shuffledList[n], shuffledList[k]);
        }

        return shuffledList;
    }

    private void UpdateAppearance(EntityUid uid, SharedPylonComponent comp)
    {
        AppearanceComponent? appearance = null;
        if (!Resolve(uid, ref appearance, false))
            return;

        _appearance.SetData(uid, PylonVisuals.Activated, comp.Activated, appearance);
    }
}

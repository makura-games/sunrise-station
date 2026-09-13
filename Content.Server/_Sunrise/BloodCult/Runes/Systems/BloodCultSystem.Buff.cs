using System.Linq;
using System.Numerics;
using Content.Server._Sunrise.BloodCult.GameRule;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Sunrise.BloodCult.Runes.Systems
{
    public partial class BloodCultSystem
    {
        public void InitializeBuffSystem()
        {
            SubscribeLocalEvent<CultBuffComponent, ComponentAdd>(OnAdd);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            UpdateBuffTimers(frameTime);
            AnyCultistNearTile();
            RemoveExpiredBuffs();
        }

        private void AnyCultistNearTile()
        {
            var cultists = EntityQuery<BloodCultistComponent>();

            foreach (var cultist in cultists)
            {
                var uid = cultist.Owner;

                if (HasComp<CultBuffComponent>(uid))
                    continue;

                if (!AnyCultTilesNearby(uid))
                    continue;

                var comp = EnsureComp<CultBuffComponent>(uid);
                comp.BuffTime = CultBuffComponent.CultTileBuffTime;
            }
        }

        private void OnAdd(EntityUid uid, CultBuffComponent comp, ComponentAdd args)
        {
            _alertsSystem.ShowAlert(uid, comp.BuffAlert);
        }

        private void UpdateBuffTimers(float frameTime)
        {
            var buffs = EntityQuery<CultBuffComponent>();

            foreach (var buff in buffs)
            {
                var uid = buff.Owner;
                var remainingTime = buff.BuffTime;

                remainingTime -= TimeSpan.FromSeconds(frameTime);

                if (TryComp<BloodCultistComponent>(uid, out var cultist))
                {
                    if (remainingTime < CultBuffComponent.CultTileBuffTime && AnyCultTilesNearby(uid))
                        remainingTime = CultBuffComponent.CultTileBuffTime;
                }

                buff.BuffTime = remainingTime;
            }
        }


        private bool AnyCultTilesNearby(EntityUid uid)
        {
            var xform = Transform(uid);
            var localpos = xform.Coordinates.Position;

            if (!TryComp<BloodCultistComponent>(uid, out var cultist))
                return false;

            var radius = CultBuffComponent.NearbyTilesBuffRadius;

            if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
                return false;

            var tilesRefs = _map.GetLocalTilesIntersecting(xform.GridUid.Value, grid, new Box2(localpos + new Vector2(-radius, -radius),
                localpos + new Vector2(radius, radius)));
            var cultTileDef = (ContentTileDefinition)_tileDefinition[$"{CultTilePrototypeId}"];
            var cultTile = new Tile(cultTileDef.TileId);

            return tilesRefs.Any(tileRef => tileRef.Tile.TypeId == cultTile.TypeId);
        }

        private void RemoveExpiredBuffs()
        {
            var buffs = EntityQuery<CultBuffComponent>();

            foreach (var buff in buffs)
            {
                var uid = buff.Owner;
                var remainingTime = buff.BuffTime;

                if (remainingTime <= TimeSpan.Zero)
                {
                    RemComp<CultBuffComponent>(uid);
                    _alertsSystem.ClearAlert(uid, buff.BuffAlert);
                }
            }
        }
    }
}

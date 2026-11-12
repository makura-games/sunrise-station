using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Utility;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Explosion;

public sealed class ExplosionDamageTest : GameTest
{
    private const string TestExplosion = "SunriseTestExplosionWithoutVariance";
    private const string TestTarget = "SunriseTestExplosionDamageTarget";
    private static readonly ProtoId<DamageTypePrototype> StructuralDamage = "Structural";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: explosion
  id: {TestExplosion}
  damagePerIntensity:
    types:
      Structural: 10
  tileBreakChance: [0]
  tileBreakIntensity: [0]

- type: entity
  id: {TestTarget}
  name: explosion damage target
  components:
  - type: Damageable
  - type: Injurable
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
";

    [Test]
    public async Task ExplosionDamageIgnoresGlobalVariance()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var config = server.ResolveDependency<IConfigurationManager>();
        var explosion = server.System<ExplosionSystem>();
        var damageableSystem = server.System<DamageableSystem>();
        var transform = server.System<SharedTransformSystem>();
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            config.SetCVar(SunriseCCVars.DamageNegativeVariance, 0.3f);
            config.SetCVar(SunriseCCVars.DamagePositiveVariance, 0f);
            var tileCenter = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
            target = server.EntMan.SpawnEntity(TestTarget, tileCenter);
        });

        await server.WaitRunTicks(1);

        await server.WaitPost(() =>
        {
            var lookup = server.EntMan.GetComponent<BroadphaseComponent>(map.Grid.Owner);
            var damageType = server.ProtoMan.Index(StructuralDamage);
            var damage = new DamageSpecifier(damageType, FixedPoint2.New(10));
            var targetTile = transform.GetGridTilePositionOrDefault(target, map.Grid.Comp);
            explosion.ExplodeTile(
                lookup,
                map.Grid,
                targetTile,
                throwForce: 0f,
                damage,
                map.MapCoords,
                new HashSet<EntityUid>(),
                TestExplosion,
                fireStacks: null,
                temperature: null,
                currentIntensity: 1f,
                cause: null);
        });

        await server.WaitAssertion(() =>
        {
            var damageable = server.EntMan.GetComponent<DamageableComponent>(target);
            Assert.That(damageableSystem.GetTotalDamage((target, damageable)), Is.EqualTo(FixedPoint2.New(10)));
        });
    }
}

#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Antag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player; // Sunrise-Edit: Серверная dummy-сессия для проверки гост-ролей без клиента.
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed partial class GhostRoleTest : GameTest
{
    [SidedDependency(Side.Server)] private AntagSelectionSystem _antagSelection = default!;
    [SidedDependency(Side.Server)] private GameTicker _ticker = default!;
    [SidedDependency(Side.Server)] private GhostRoleSystem _ghostRole = default!;

    private static string[] _antagGameRules = GameDataScrounger.EntitiesWithComponent("AntagSelection");

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false, // Sunrise-Edit: Для правил нужен настоящий GameTicker.
        InLobby = true, // Sunrise-Edit: Неподключённый к сущности игрок сможет занимать гост-роли без latejoin-спавна.
        Map = PoolManager.TestStation
    };

    [Test]
    [TestOf(typeof(GameTicker)), TestOf(typeof(AntagSelectionSystem)), TestOf(typeof(AntagSelectionComponent)), TestOf(typeof(GhostRoleSystem))]
    [Description("Ensures all GameRule entities with AntagSelectionComponent can properly spawn those roles and they can be taken.")]
    public async Task TestAntagGhostRoles()
    {
        // Sunrise edit start - проверяем все правила на одной серверной сессии вместо запуска пары для каждого правила.
        var serverSession = ServerSession!;

        await Pair.Server.WaitPost(() =>
        {
            _ticker.StartRound(true);
            _ticker.ClearGameRules();
        });
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound),
                "The shared ghost-role test round failed to start.");
        });

        foreach (var ruleId in _antagGameRules)
        {
            await Pair.Server.WaitAssertion(() =>
            {
                // Не даём следующему правилу назначить роль напрямую уже захватившему гост-роль игроку.
                Pair.Server.PlayerMan.SetAttachedEntity(serverSession, null);
                TestAntagGhostRole(ruleId, serverSession);
            });
            // Сервер должен обработать отложенное удаление сущностей предыдущего правила.
            await Pair.RunTicksSync(1);
        }
        // Sunrise edit end
    }

    private void TestAntagGhostRole(string ruleId, ICommonSession serverSession) // Sunrise-Edit
    {
        var rule = SProtoMan.Index<EntityPrototype>(ruleId);
        Assert.That(rule.TryGetComponent<AntagSelectionComponent>(out var antag, SEntMan.ComponentFactory), Is.True,
            $"Game rule {ruleId} has no {nameof(AntagSelectionComponent)}.");

        var playerCount = _antagSelection.GetActivePlayerCount();
        Assert.That(playerCount, Is.Zero,
            $"Game rule {ruleId} must be tested without an active player so that all remaining roles become ghost roles.");

        Assert.That(_ticker.StartGameRule(ruleId, out var gameRule), Is.True,
            $"Game rule {ruleId} failed to start.");

        Dictionary<ProtoId<AntagSpecifierPrototype>, int> rules = [];
        var runningCount = 0;

        foreach (var selector in antag!.Antags)
        {
            var specifier = SProtoMan.Index(selector.Proto);
            var count = _antagSelection.GetTargetAntagCount(selector, playerCount, ref runningCount);

            if (specifier.SpawnerPrototype == null)
                continue;

            var value = rules.GetValueOrDefault(specifier);
            rules[selector.Proto] = value + count;
        }

        var roleEnumerator = SEntMan.EntityQueryEnumerator<GhostRoleAntagSpawnerComponent, GhostRoleComponent, TransformComponent>();
        while (roleEnumerator.MoveNext(out var spawner, out var role, out var xform))
        {
            // Предыдущие правила уже завершены, но их сущности удалятся только при очистке пары.
            if (spawner.Rule != gameRule)
                continue;

            // Ensure the ghost role spawner spawned correctly!
            Assert.That(spawner.Definition, Is.Not.Null);
            Assert.That(xform.MapUid, Is.Not.Null);
            Assert.That(xform.MapID, Is.Not.EqualTo(MapId.Nullspace));

            Assert.That(rules.TryGetValue(spawner.Definition.Value, out var value), Is.True,
                $"Game rule {ruleId} spawned an unexpected antag specifier {spawner.Definition.Value}.");
            rules[spawner.Definition.Value] = value - 1;

            // Take the ghost role and ensure we take it!
            Assert.That(_ghostRole.Takeover(serverSession, role.Identifier), Is.True,
                $"Failed to take ghost role {spawner.Definition.Value} from game rule {ruleId}.");
            Assert.That(serverSession.AttachedEntity, Is.Not.Null);

            // Ensure we spawned in the correct location
            var sessionXform = SEntMan.GetComponent<TransformComponent>(serverSession.AttachedEntity.Value);
            Assert.That(sessionXform.MapUid, Is.EqualTo(xform.MapUid));

            // We break it up like this cause otherwise it'll sometimes randomly fail
            // TODO: Engine IEquatable for EntityCoordinates
            Assert.That(sessionXform.Coordinates.EntityId, Is.EqualTo(xform.Coordinates.EntityId));

            // I will not get heisentest due to floating point errors
            Assert.That(MathHelper.CloseTo(sessionXform.Coordinates.X, xform.Coordinates.X, 0.001f), Is.True);
            Assert.That(MathHelper.CloseTo(sessionXform.Coordinates.Y, xform.Coordinates.Y, 0.001f), Is.True);
        }

        // Ensure all ghost roles spawned and were assigned!!!
        foreach (var (proto, count) in rules)
        {
            Assert.That(count, Is.Zero,
                $"Game rule {ruleId} left {count} unassigned ghost roles for antag specifier {proto}.");
        }

        // End all rules
        _ticker.ClearGameRules();
        Assert.That(_ticker.GetAddedGameRules(), Is.Empty);
    }
}

using System.Linq;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class VampireRuleTest
{
    private const string VampireGameRuleProtoId = "Vampire";

    [Test]
    public async Task TestVampireGameRulePrototype()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings()
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });
        var server = pair.Server;
        var protoMan = server.ProtoMan;
        var compFact = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.TryIndex<EntityPrototype>(VampireGameRuleProtoId, out var gameRuleEnt),
                $"Failed to lookup vampire game rule entity prototype with ID \"{VampireGameRuleProtoId}\"!");

            Assert.That(gameRuleEnt.TryGetComponent<GameRuleComponent>(out var gameRule, compFact),
                $"Game rule entity {VampireGameRuleProtoId} does not have a GameRuleComponent!");

            var vampRuleComp = compFact.GetComponent<Content.Server._Sunrise.GameTicking.Rules.Components.VampireRuleComponent>();
            Assert.That(vampRuleComp != null, "VampireRuleComponent not registered!");

            var vampComp = compFact.GetComponent<Content.Shared._Sunrise.Antags.Vampires.Components.VampireComponent>();
            Assert.That(vampComp != null, "VampireComponent not registered!");
        });
    }
}

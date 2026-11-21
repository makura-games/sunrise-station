---
name: ss14-tests-authoring
description: Practical workflow for writing your own unit/integration tests in the Space Station 14 architecture: from choosing a strategy to stable assertions and test maintenance.
---

# Writing your own tests in SS14

## When to use

Use this skill when needed:
- design a new test script from scratch;
- select the test type (unit, server-only integration, server+client integration);
- design the test so that it is stable and does not break the pool;
- quickly review the test for anti-patterns before launching CI.

The internals of `PoolManager/TestPair` are discussed in detail in `ss14-tests-poolmanager`. Here the emphasis is on the author's workflow and the quality of tests as an artifact.

## Short decision tree

1. Are you testing pure logic without network/tick/IoC?
- Yes: unit test.
- No: integration test.

2. Do you need real synchronization client↔server, UI/BUI, prediction or input?
- Yes: server+client integration.
- No: server-only integration may be simpler and faster.

3. Does the test change the global state, which does not survive reuse well?
- Yes: `Dirty = true` or dotted `Pool = false` for isolation.
- No: leave reuse, it greatly speeds up the wound.

## Basic workflow of the author 🧪

1. Fix the invariants:
- what needs to change;
- what should not change;
- on which side (server/client) each condition is checked.

2. Select minimal environment settings:
- `Connected`, if you need a live client;
- `DummyTicker = false`, if you need real round-flow;
- `InLobby = true`, if the lobby logic is checked;
- `Dirty = true`, only if the test really spoils the reuse state.

3. Arrange:
- create a map/entities/components in `WaitPost(...)`;
- if necessary, connect test prototypes (`[TestPrototypes]` or `ExtraPrototypes`).

4. Act:
- perform an action through the system/input/UI;
- after the action, give the necessary ticks for processing.

5. Assert:
- check in `WaitAssertion(...)` or outside the callback after synchronization;
- for server+client always take into account the tick delta.

6. Cleanup:
- always `CleanReturnAsync()` for pairs from the pool;
- if you raised separate server/client instances, disconnect/shutdown correctly.

## Pattern 1: standard content integration test

```csharp
[Test]
public async Task MyScenario_Works()
{
    await using var pair = await PoolManager.GetServerClient(new PoolSettings
    {
        Connected = true,
        Dirty = false,
    });

    var server = pair.Server;
    EntityUid subject = default;

    await server.WaitPost(() =>
    {
        // Arrange: create a test entity.
        subject = server.EntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
    });

    await pair.RunTicksSync(5);
    await pair.SyncTicks(targetDelta: 1);

    await server.WaitAssertion(() =>
    {
        // Assert: check after stabilization.
        Assert.That(server.EntMan.EntityExists(subject), Is.True);
    });

    await pair.CleanReturnAsync();
}
```

## Pattern 2: inline prototypes inside a test

```csharp
[TestPrototypes]
private const string Prototypes = @"
- type: entity
  id: MyTestTarget
  components:
  - type: Physics
";

[Test]
public async Task UsesInlinePrototype()
{
    await using var pair = await PoolManager.GetServerClient();

    await pair.Server.WaitAssertion(() =>
    {
        Assert.That(pair.Server.ProtoMan.HasIndex<EntityPrototype>("MyTestTarget"), Is.True);
    });

    await pair.CleanReturnAsync();
}
```

## Pattern 3: isolated special case without a pool

Use when the case involves global registrations/deep isolation.

```csharp
var server = StartServer(new ServerIntegrationOptions
{
    Pool = false,
    ExtraPrototypes = Prototypes,
});
var client = StartClient(new ClientIntegrationOptions
{
    Pool = false,
    ExtraPrototypes = Prototypes,
});

await Task.WhenAll(server.WaitIdleAsync(), client.WaitIdleAsync());
Assert.DoesNotThrow(() => client.SetConnectTarget(server));
await client.WaitPost(() => client.ResolveDependency<IClientNetManager>().ClientConnect(null!, 0, null!));
```

## Good test patterns :)

- Each test checks 1 behavioral contract (not “all at once”).
- Arrange/Act/Assert are visually separated.
- All mutations are wrapped in `WaitPost`, all checks are wrapped in `WaitAssertion`.
- There is an explicit synchronization of ticks before cross-side assertions.
- Complex scripts rely on helper methods (input, do-after, UI) instead of copy-paste.
- The test name describes the observed behavior (`X_WhenY_ShouldZ` or equivalent).

## Anti-patterns

- “Giant” test with dozens of independent goals.
- Magical `RunTicks(123)` without explanation why exactly so much.
- `Dirty = true` and `Pool = false` default for no reason.
- Assertions in `Post(...)` and mutations in `WaitAssertion(...)`.
- Reliance on documentation contrary to current code.
- Ignoring cleanup and hoping for automatic dispose 😬

## Examples from actual code

### Example A: scenario where lobby/in-round transitions are needed

```csharp
await using var pair = await PoolManager.GetServerClient(new PoolSettings
{
    Dirty = true,       // the test changes the phase of the game
    DummyTicker = false,
    Connected = true,
    InLobby = true,
});

var ticker = pair.Server.System<GameTicker>();
await pair.Server.WaitAssertion(() =>
{
    Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
});
```

### Example B: Testing Interaction via System Call

```csharp
await server.WaitPost(() =>
{
    // Act: server-side user interaction with the target.
    interactionSystem.UserInteraction(user, server.Transform(target).Coordinates, target);
});

await server.WaitAssertion(() =>
{
    // Assert: the expected handler actually worked.
    Assert.That(interactHand, Is.True);
});
```

### Example C: UI/BUI steps considering network RTT

```csharp
await client.WaitPost(() => bui.SendMessage(message));

// We give time for client->server->client processing.
await pair.RunTicksSync(15);

await client.WaitAssertion(() =>
{
    Assert.That(expectedUiState, Is.True);
});
```

## Mini checklist before PR

- The test runs locally for at least 3 runs in a row.
- There is no direct dependence on the order in which other tests are performed.
- No hidden mutation of global state without rollback.
- Cleanup is explicit and correct.
- The environment parameters are minimal (nothing extra).

## Practical notes from docs

- `COMPlus_gcServer=1` usually speeds up integration wounds.
- User data is in in-memory integration mode and is not saved between runs.

Use this as an operational note, but always check the final decision against the current implementation of the code.

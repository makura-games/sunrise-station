using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._Sunrise.Tutorial.Conditions;
using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Content.Shared._Sunrise.Tutorial.Events;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Objectives;

[TestFixture]
[TestOf(typeof(ObjectiveSystem))]
public sealed class ObjectiveSystemTest
{
    private const string NamedObjectiveId = "SunriseObjectiveIntegrationNamed";
    private const string InvalidNamedObjectiveId = "SunriseObjectiveIntegrationInvalid";

    [TestPrototypes]
    private const string NamedObjectivePrototypes = """
        - type: objective
          id: SunriseObjectiveIntegrationNamed
          definition:
            all:
            - !type:PickupObjectiveCondition
              id: pickup

        - type: objective
          id: SunriseObjectiveIntegrationInvalid
          definition:
            all:
            - !type:PickupObjectiveCondition
        """;

    [TestPrototypes]
    private const string TutorialFlowPrototypes = """
        - type: tutorialSequence
          id: SunriseObjectiveTutorialFlow
          duration: 60
          estimatedDuration:
            minimum: 1
            maximum: 60
          grid: /Maps/_Sunrise/Tutorials/introduction_tutorial.yml
          playerEntity: MobHuman
          steps:
          - SunriseObjectiveTutorialMain
          - SunriseObjectiveTutorialNext

        - type: tutorialStep
          id: SunriseObjectiveTutorialMain
          completion:
            all:
            - !type:PickupObjectiveCondition
          failures:
          - when:
              all:
              - !type:PickupObjectiveCondition
            repairStep: SunriseObjectiveTutorialRepair

        - type: tutorialStep
          id: SunriseObjectiveTutorialRepair
          completion:
            all:
            - !type:PickupObjectiveCondition

        - type: tutorialStep
          id: SunriseObjectiveTutorialNext
          completion:
            all:
            - !type:ElapsedTimeObjectiveCondition
              delay: 60

        - type: tutorialSequence
          id: SunriseObjectiveTutorialPreconditions
          duration: 60
          estimatedDuration:
            minimum: 1
            maximum: 60
          grid: /Maps/_Sunrise/Tutorials/introduction_tutorial.yml
          playerEntity: MobHuman
          steps:
          - SunriseObjectiveTutorialPreconditionMain
          - SunriseObjectiveTutorialPreconditionFail

        - type: tutorialStep
          id: SunriseObjectiveTutorialPreconditionMain
          completion:
            all:
            - !type:ElapsedTimeObjectiveCondition
              delay: 60
          preconditions:
            all:
            - !type:CombatModeObjectiveCondition
          preconditionFailStep: SunriseObjectiveTutorialPreconditionFail

        - type: tutorialStep
          id: SunriseObjectiveTutorialPreconditionFail
          completion:
            all:
            - !type:ElapsedTimeObjectiveCondition
              delay: 60
        """;

    [Test]
    public async Task InlineGraphSupportsAllAnyAndInversion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var objectives = server.System<ObjectiveSystem>();

        var owner = EntityUid.Invalid;
        var objective = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            owner = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            var definition = new ObjectiveDefinition
            {
                All =
                [
                    new PickupObjectiveCondition(),
                ],
                Any =
                [
                    new PickupObjectiveCondition { Inverted = true },
                ],
            };

            Assert.That(objectives.TryStartObjective(
                owner,
                definition,
                MonitorOptions(),
                out objective), Is.True);
            AssertStatus(objectives, objective, active: true, satisfied: false, completed: false);

            Assert.That(objectives.TryAddConditionProgress(new ObjectiveConditionHandle(objective, "all:0"), 1),
                Is.True);
            AssertStatus(objectives, objective, active: true, satisfied: true, completed: false);

            Assert.That(objectives.TryAddConditionProgress(new ObjectiveConditionHandle(objective, "any:0"), 1),
                Is.True);
            AssertStatus(objectives, objective, active: true, satisfied: false, completed: false);
        });

        await server.WaitAssertion(() => Assert.That(objectives.TryStopObjective(objective), Is.True));
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.Deleted(objective), Is.True);
            Assert.That(entityManager.HasComponent<ObjectiveOwnerComponent>(owner), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ValidationRejectsEmptyDefinitionsDuplicateIdsAndInvalidCounts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var objectives = server.System<ObjectiveSystem>();

        await server.WaitAssertion(() =>
        {
            var owner = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);

            Assert.That(objectives.TryStartObjective(
                owner,
                new ObjectiveDefinition(),
                MonitorOptions(),
                out _), Is.False);

            var duplicateIds = new ObjectiveDefinition
            {
                All =
                [
                    new PickupObjectiveCondition { Id = "duplicate" },
                ],
                Any =
                [
                    new PickupObjectiveCondition { Id = "duplicate" },
                ],
            };
            Assert.That(objectives.TryStartObjective(owner, duplicateIds, MonitorOptions(), out _), Is.False);

            var negativeCount = new ObjectiveDefinition
            {
                All =
                [
                    new PickupObjectiveCondition { Count = -1 },
                ],
            };
            Assert.That(objectives.TryStartObjective(owner, negativeCount, MonitorOptions(), out _), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StateApiIsReversibleAndTransferPreservesHistory()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var objectives = server.System<ObjectiveSystem>();
        var firstOwner = EntityUid.Invalid;
        var secondOwner = EntityUid.Invalid;
        var objective = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            firstOwner = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            secondOwner = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            var definition = new ObjectiveDefinition
            {
                All =
                [
                    new PickupObjectiveCondition { Count = 2 },
                ],
            };

            Assert.That(objectives.TryStartObjective(
                firstOwner,
                definition,
                MonitorOptions(),
                out objective), Is.True);
            var handle = new ObjectiveConditionHandle(objective, "all:0");

            Assert.That(objectives.TrySetConditionSatisfied(handle, true), Is.True);
            AssertStatus(objectives, objective, active: true, satisfied: true, completed: false);
            Assert.That(objectives.TrySetConditionSatisfied(handle, false), Is.True);
            AssertStatus(objectives, objective, active: true, satisfied: false, completed: false);

            Assert.That(objectives.TryAddConditionProgress(handle, 1), Is.True);
            Assert.That(objectives.TryTransferObjective(objective, secondOwner), Is.True);
            Assert.That(objectives.TryGetConditionStatus(handle, out var conditionStatus), Is.True);
            Assert.That(conditionStatus.Progress, Is.EqualTo(1));

            entityManager.DeleteEntity(firstOwner);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            AssertStatus(objectives, objective, active: true, satisfied: false, completed: false);
            entityManager.DeleteEntity(secondOwner);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() => Assert.That(entityManager.Deleted(objective), Is.True));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NamedDefinitionsAllowMissingPresentationAndRequireStableConditionIds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var objectives = server.System<ObjectiveSystem>();
        var prototype = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var named = prototype.Index<ObjectivePrototype>(NamedObjectiveId);
            Assert.Multiple(() =>
            {
                Assert.That(named.Name, Is.Null);
                Assert.That(named.Description, Is.Null);
            });

            var owner = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            Assert.That(objectives.TryStartObjective(
                owner,
                new ProtoId<ObjectivePrototype>(NamedObjectiveId),
                MonitorOptions(),
                out var objective), Is.True);
            Assert.That(objectives.TryGetCondition<PickupObjectiveCondition>(
                new ObjectiveConditionHandle(objective, "pickup"),
                out _), Is.True);

            Assert.That(objectives.TryStartObjective(
                owner,
                new ProtoId<ObjectivePrototype>(InvalidNamedObjectiveId),
                MonitorOptions(),
                out _), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PickupCountAndInventoryStateUseGameplaySystems()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var objectives = server.System<ObjectiveSystem>();
        var combat = server.System<SharedCombatModeSystem>();
        var hands = server.System<SharedHandsSystem>();
        var inventory = server.System<InventorySystem>();
        var map = await pair.CreateTestMap();

        var owner = EntityUid.Invalid;
        var pickupObjective = EntityUid.Invalid;
        var inventoryObjective = EntityUid.Invalid;
        var combatObjective = EntityUid.Invalid;
        var internalsObjective = EntityUid.Invalid;
        var breathTool = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            var coordinates = map.GridCoords;
            owner = entityManager.SpawnEntity("MobHuman", coordinates);
            var firstCrowbar = entityManager.SpawnEntity("Crowbar", coordinates);
            var secondCrowbar = entityManager.SpawnEntity("Crowbar", coordinates);

            var pickupDefinition = new ObjectiveDefinition
            {
                All =
                [
                    new PickupObjectiveCondition
                    {
                        Target = "Crowbar",
                        Count = 2,
                    },
                ],
            };
            Assert.That(objectives.TryStartObjective(
                owner,
                pickupDefinition,
                OneShotRetainedOptions(),
                out pickupObjective), Is.True);

            Assert.That(hands.TryPickupAnyHand(owner, firstCrowbar, checkActionBlocker: false, animate: false), Is.True);
            AssertStatus(objectives, pickupObjective, active: true, satisfied: false, completed: false);
            Assert.That(hands.TryPickupAnyHand(owner, secondCrowbar, checkActionBlocker: false, animate: false), Is.True);
            AssertStatus(objectives, pickupObjective, active: false, satisfied: true, completed: true);

            var secondaryTargetDefinition = new ObjectiveDefinition
            {
                All =
                [
                    new UseObjectiveCondition { Target = "Crowbar" },
                ],
            };
            Assert.That(objectives.TryStartObjective(
                owner,
                secondaryTargetDefinition,
                OneShotRetainedOptions(),
                out var secondaryTargetObjective), Is.True);
            var interactionTarget = entityManager.SpawnEntity(null, coordinates);
            entityManager.EventBus.RaiseLocalEvent(
                owner,
                new UserInteractUsingEvent(owner, firstCrowbar, interactionTarget, coordinates));
            AssertStatus(objectives, secondaryTargetObjective, active: false, satisfied: true, completed: true);

            var inventoryDefinition = new ObjectiveDefinition
            {
                All =
                [
                    new InventorySlotContainsObjectiveCondition
                    {
                        Slot = SlotFlags.HEAD,
                        Item = "ClothingHeadHatHardhatYellow",
                    },
                ],
            };
            Assert.That(objectives.TryStartObjective(
                owner,
                inventoryDefinition,
                MonitorOptions(),
                out inventoryObjective), Is.True);
            AssertStatus(objectives, inventoryObjective, active: true, satisfied: false, completed: false);

            var hardhat = entityManager.SpawnEntity("ClothingHeadHatHardhatYellow", coordinates);
            Assert.That(inventory.TryEquip(owner, hardhat, "head", silent: true, force: true), Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            AssertStatus(objectives, inventoryObjective, active: true, satisfied: true, completed: false);
            Assert.That(inventory.TryUnequip(owner, "head", silent: true, force: true), Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            AssertStatus(objectives, inventoryObjective, active: true, satisfied: false, completed: false);

            var combatDefinition = new ObjectiveDefinition
            {
                All =
                [
                    new CombatModeObjectiveCondition(),
                ],
            };
            Assert.That(objectives.TryStartObjective(
                owner,
                combatDefinition,
                MonitorOptions(),
                out combatObjective), Is.True);
            AssertStatus(objectives, combatObjective, active: true, satisfied: false, completed: false);
            combat.SetInCombatMode(owner, true);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            AssertStatus(objectives, combatObjective, active: true, satisfied: true, completed: false);
            combat.SetInCombatMode(owner, false);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            AssertStatus(objectives, combatObjective, active: true, satisfied: false, completed: false);

            var internalsDefinition = new ObjectiveDefinition
            {
                All =
                [
                    new InternalsEnabledObjectiveCondition(),
                ],
            };
            Assert.That(objectives.TryStartObjective(
                owner,
                internalsDefinition,
                MonitorOptions(),
                out internalsObjective), Is.True);
            AssertStatus(objectives, internalsObjective, active: true, satisfied: false, completed: false);

            breathTool = entityManager.SpawnEntity("ClothingMaskBreath", map.GridCoords);
            var gasTank = entityManager.SpawnEntity("OxygenTankFilled", map.GridCoords);
            var internals = entityManager.EnsureComponent<InternalsComponent>(owner);
            var tool = entityManager.GetComponent<BreathToolComponent>(breathTool);
            internals.BreathTools.Add(breathTool);
            internals.GasTankEntity = gasTank;
            tool.ConnectedInternalsEntity = owner;
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            AssertStatus(objectives, internalsObjective, active: true, satisfied: true, completed: false);
            entityManager.GetComponent<BreathToolComponent>(breathTool).ConnectedInternalsEntity = null;
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
            AssertStatus(objectives, internalsObjective, active: true, satisfied: false, completed: false));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TimerTravelAndObserverBindingsAreIsolatedPerInstance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var objectives = server.System<ObjectiveSystem>();
        var transform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();
        var otherMap = await pair.CreateTestMap();

        var owner = EntityUid.Invalid;
        var observed = EntityUid.Invalid;
        var firstObserverObjective = EntityUid.Invalid;
        var secondObserverObjective = EntityUid.Invalid;
        var thirdObserverObjective = EntityUid.Invalid;
        var travelObjective = EntityUid.Invalid;
        var reachObjective = EntityUid.Invalid;
        ObjectiveDefinition observeCrowbar = default!;

        await server.WaitAssertion(() =>
        {
            var coordinates = map.GridCoords;
            owner = entityManager.SpawnEntity("MobHuman", coordinates);
            observed = entityManager.SpawnEntity("Crowbar", coordinates);
            var tutorial = entityManager.EnsureComponent<TutorialPlayerComponent>(owner);
            tutorial.Grid = map.Grid;

            var marker = entityManager.SpawnEntity("TutorialGoalMarker", coordinates);
            transform.SetLocalPosition(marker, new Vector2(3f, 0f));
            entityManager.SpawnEntity("TutorialGoalMarker", otherMap.GridCoords);

            var elapsed = new ObjectiveDefinition
            {
                All =
                [
                    new ElapsedTimeObjectiveCondition { Delay = TimeSpan.Zero },
                ],
            };
            Assert.That(objectives.TryStartObjective(
                owner,
                elapsed,
                OneShotRetainedOptions(),
                out var elapsedObjective), Is.True);
            AssertStatus(objectives, elapsedObjective, active: false, satisfied: true, completed: true);

            var travel = new ObjectiveDefinition
            {
                All =
                [
                    new TravelDistanceObjectiveCondition { Distance = 2f },
                ],
            };
            Assert.That(objectives.TryStartObjective(
                owner,
                travel,
                MonitorOptions(),
                out travelObjective), Is.True);

            var reach = new ObjectiveDefinition
            {
                All =
                [
                    new ReachMarkerObjectiveCondition
                    {
                        Marker = "TutorialGoalMarker",
                        Distance = 1.5f,
                    },
                ],
            };
            Assert.That(objectives.TryStartObjective(
                owner,
                reach,
                MonitorOptions(),
                out reachObjective), Is.True);
            AssertStatus(objectives, reachObjective, active: true, satisfied: false, completed: false);

            observeCrowbar = new ObjectiveDefinition
            {
                All =
                [
                    new PickupObjectiveCondition { Target = "Crowbar" },
                ],
            };
            Assert.That(objectives.TryStartObjective(
                owner,
                observeCrowbar,
                MonitorOptions(),
                out firstObserverObjective), Is.True);
            Assert.That(objectives.TryStartObjective(
                owner,
                observeCrowbar,
                MonitorOptions(),
                out secondObserverObjective), Is.True);

            var observer = entityManager.GetComponent<ObjectiveInteractionObserverComponent>(observed);
            Assert.That(observer.Registrations, Has.Count.EqualTo(2));

            transform.SetLocalPosition(owner, new Vector2(3f, 0f));
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            AssertStatus(objectives, travelObjective, active: true, satisfied: true, completed: false);
            AssertStatus(objectives, reachObjective, active: true, satisfied: true, completed: false);

            Assert.That(objectives.TryStopObjective(firstObserverObjective), Is.True);
            Assert.That(entityManager.GetComponent<ObjectiveInteractionObserverComponent>(observed).Registrations,
                Has.Count.EqualTo(1));
            Assert.That(objectives.TryStopObjective(secondObserverObjective), Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<ObjectiveInteractionObserverComponent>(observed), Is.False);
            Assert.That(objectives.TryStartObjective(
                owner,
                observeCrowbar,
                MonitorOptions(),
                out thirdObserverObjective), Is.True);
            Assert.That(entityManager.HasComponent<ObjectiveInteractionObserverComponent>(observed), Is.True);
            entityManager.DeleteEntity(observed);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            AssertStatus(objectives, thirdObserverObjective, active: true, satisfied: false, completed: false);
            entityManager.DeleteEntity(owner);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
            Assert.That(entityManager.Deleted(thirdObserverObjective), Is.True));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TargetPickedUpAfterActivationIsObservedAndCleanedUp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var objectives = server.System<ObjectiveSystem>();
        var hands = server.System<SharedHandsSystem>();
        var map = await pair.CreateTestMap();

        var owner = EntityUid.Invalid;
        var item = EntityUid.Invalid;
        var objective = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            owner = entityManager.SpawnEntity("MobHuman", map.GridCoords);
            item = entityManager.SpawnEntity("Crowbar", map.GridCoords);
            var definition = new ObjectiveDefinition
            {
                All =
                [
                    new UseObjectiveCondition { Target = "Crowbar" },
                ],
            };
            var options = OneShotRetainedOptions();
            options.ObservationScope = ObjectiveObservationScope.Hands;

            Assert.That(objectives.TryStartObjective(owner, definition, options, out objective), Is.True);
            Assert.That(entityManager.HasComponent<ObjectiveInteractionObserverComponent>(item), Is.False);
            Assert.That(hands.TryPickupAnyHand(owner, item, checkActionBlocker: false, animate: false), Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<ObjectiveInteractionObserverComponent>(item), Is.True);
            entityManager.EventBus.RaiseLocalEvent(item, new UseInHandEvent(owner));
            AssertStatus(objectives, objective, active: false, satisfied: true, completed: true);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
            Assert.That(entityManager.HasComponent<ObjectiveInteractionObserverComponent>(item), Is.False));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UiReportsRequireActiveTutorialAndMatchingHandle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var objectives = server.System<ObjectiveSystem>();
        var buttons = server.System<UiButtonPressedObjectiveConditionSystem>();
        var visibility = server.System<UiControlVisibleObjectiveConditionSystem>();

        await server.WaitAssertion(() =>
        {
            var player = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            entityManager.EnsureComponent<TutorialPlayerComponent>(player).TutorialInitialized = true;

            var buttonDefinition = new ObjectiveDefinition
            {
                All =
                [
                    new UiButtonPressedObjectiveCondition
                    {
                        Button = "ExpectedButton",
                        Selectors =
                        [
                            new UiByName { Name = "ExpectedButton" },
                        ],
                    },
                ],
            };
            Assert.That(objectives.TryStartObjective(
                player,
                buttonDefinition,
                TutorialOneShotOptions(),
                out var buttonObjective), Is.True);

            Assert.That(buttons.TryRecordButton(player, "ForgedButton"), Is.False);
            AssertStatus(objectives, buttonObjective, active: true, satisfied: false, completed: false);
            Assert.That(buttons.TryRecordButton(player, "ExpectedButton"), Is.True);
            AssertStatus(objectives, buttonObjective, active: false, satisfied: true, completed: true);

            var visibleDefinition = new ObjectiveDefinition
            {
                All =
                [
                    new UiControlVisibleObjectiveCondition
                    {
                        Control = "ExpectedControl",
                        Selectors =
                        [
                            new UiByName { Name = "ExpectedControl" },
                        ],
                    },
                ],
            };
            Assert.That(objectives.TryStartObjective(
                player,
                visibleDefinition,
                TutorialOneShotOptions(),
                out var visibleObjective), Is.True);
            Assert.That(visibility.TryRecordVisible(player, "ForgedControl"), Is.False);
            AssertStatus(objectives, visibleObjective, active: true, satisfied: false, completed: false);
            Assert.That(visibility.TryRecordVisible(player, "ExpectedControl"), Is.True);
            AssertStatus(objectives, visibleObjective, active: false, satisfied: true, completed: true);

            var nonTutorialPlayer = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            Assert.That(objectives.TryStartObjective(
                nonTutorialPlayer,
                buttonDefinition,
                TutorialOneShotOptions(),
                out var unauthorizedObjective), Is.True);
            Assert.That(buttons.TryRecordButton(nonTutorialPlayer, "ExpectedButton"), Is.False);
            AssertStatus(objectives, unauthorizedObjective, active: true, satisfied: false, completed: false);

            var nonTutorialOptions = TutorialOneShotOptions();
            nonTutorialOptions.SourceIdentifier = "OtherConsumer";
            Assert.That(objectives.TryStartObjective(
                player,
                buttonDefinition,
                nonTutorialOptions,
                out var otherConsumerObjective), Is.True);
            Assert.That(buttons.TryRecordButton(player, "ExpectedButton"), Is.False);
            AssertStatus(objectives, otherConsumerObjective, active: true, satisfied: false, completed: false);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TutorialFailurePrecedesCompletionAndRepairResetsProgress()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var objectives = server.System<ObjectiveSystem>();

        var player = EntityUid.Invalid;
        var preconditionPlayer = EntityUid.Invalid;
        var originalCompletion = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            player = entityManager.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var tutorial = entityManager.EnsureComponent<TutorialPlayerComponent>(player);
            tutorial.SequenceId = "SunriseObjectiveTutorialFlow";
            tutorial.TutorialInitialized = true;

            var activated = new TutorialStepActivatedEvent(player, "SunriseObjectiveTutorialMain");
            entityManager.EventBus.RaiseEvent(EventSource.Local, ref activated);

            var runtime = entityManager.GetComponent<TutorialStepObjectivesComponent>(player);
            originalCompletion = runtime.Completion;
            Assert.That(runtime.Failures, Has.Count.EqualTo(1));

            Assert.That(objectives.TryAddConditionProgress(
                new ObjectiveConditionHandle(runtime.Completion, "all:0"),
                1), Is.True);
            Assert.That(objectives.TryAddConditionProgress(
                new ObjectiveConditionHandle(runtime.Failures[0], "all:0"),
                1), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(runtime.CompletionSatisfied, Is.True);
                Assert.That(runtime.FailuresSatisfied[0], Is.True);
            });
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var tutorial = entityManager.GetComponent<TutorialPlayerComponent>(player);
            Assert.That(tutorial.ActiveStepOverride?.Id, Is.EqualTo("SunriseObjectiveTutorialRepair"));

            var repairRuntime = entityManager.GetComponent<TutorialStepObjectivesComponent>(player);
            Assert.That(repairRuntime.Completion, Is.Not.EqualTo(originalCompletion));
            Assert.That(objectives.TryAddConditionProgress(
                new ObjectiveConditionHandle(repairRuntime.Completion, "all:0"),
                1), Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var tutorial = entityManager.GetComponent<TutorialPlayerComponent>(player);
            Assert.That(tutorial.ActiveStepOverride, Is.Null);

            var restartedRuntime = entityManager.GetComponent<TutorialStepObjectivesComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(restartedRuntime.Completion, Is.Not.EqualTo(originalCompletion));
                Assert.That(restartedRuntime.CompletionSatisfied, Is.False);
            });

            Assert.That(objectives.TryAddConditionProgress(
                new ObjectiveConditionHandle(restartedRuntime.Completion, "all:0"),
                1), Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var tutorial = entityManager.GetComponent<TutorialPlayerComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(tutorial.ActiveStepOverride, Is.Null);
                Assert.That(tutorial.StepIndex, Is.EqualTo(1));
            });

            preconditionPlayer = entityManager.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var preconditionTutorial = entityManager.EnsureComponent<TutorialPlayerComponent>(preconditionPlayer);
            preconditionTutorial.SequenceId = "SunriseObjectiveTutorialPreconditions";
            preconditionTutorial.TutorialInitialized = true;

            var activated = new TutorialStepActivatedEvent(
                preconditionPlayer,
                "SunriseObjectiveTutorialPreconditionMain");
            entityManager.EventBus.RaiseEvent(EventSource.Local, ref activated);
            Assert.That(entityManager.GetComponent<TutorialStepObjectivesComponent>(preconditionPlayer)
                .PreconditionsSatisfied, Is.False);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var preconditionTutorial = entityManager.GetComponent<TutorialPlayerComponent>(preconditionPlayer);
            Assert.That(preconditionTutorial.StepIndex, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    private static ObjectiveStartOptions MonitorOptions()
    {
        return new ObjectiveStartOptions
        {
            Mode = ObjectiveRunMode.Monitor,
            CompletionRetention = ObjectiveCompletionRetention.Retain,
        };
    }

    private static ObjectiveStartOptions OneShotRetainedOptions()
    {
        return new ObjectiveStartOptions
        {
            Mode = ObjectiveRunMode.OneShot,
            CompletionRetention = ObjectiveCompletionRetention.Retain,
        };
    }

    private static ObjectiveStartOptions TutorialOneShotOptions()
    {
        return new ObjectiveStartOptions
        {
            Mode = ObjectiveRunMode.OneShot,
            CompletionRetention = ObjectiveCompletionRetention.Retain,
            SourceIdentifier = "TutorialCompletion",
        };
    }

    private static void AssertStatus(
        ObjectiveSystem objectives,
        EntityUid objective,
        bool active,
        bool satisfied,
        bool completed)
    {
        Assert.That(objectives.TryGetObjectiveStatus(objective, out var status), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(status.Active, Is.EqualTo(active));
            Assert.That(status.Satisfied, Is.EqualTo(satisfied));
            Assert.That(status.Completed, Is.EqualTo(completed));
        });
    }
}

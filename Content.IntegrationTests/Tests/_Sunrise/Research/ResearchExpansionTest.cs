using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Lathe.Prototypes;
using Content.Server.Research.Systems;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Research.Components;
using Content.Shared.Research.Events;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Research;

#pragma warning disable RA0002 // Tests prepare isolated component state directly.
[TestFixture]
public sealed class ResearchExpansionTest : GameTest
{
    private sealed class DisciplineSelectedListenerSystem : TestListenerSystem<ResearchDisciplineSelectedEvent>;

    private static readonly ProtoId<TechDisciplinePrototype> Experimental = "Experimental";
    private static readonly ProtoId<TechDisciplinePrototype> Industrial = "Industrial";
    private static readonly ProtoId<TechDisciplinePrototype> CivilianServices = "CivilianServices";
    private static readonly ProtoId<TechnologyPrototype> ResearchGrant = "SunriseResearchGrant";
    private static readonly EntProtoId TechnologyDiskTerminal = "ComputerTechnologyDiskTerminal";
    private static readonly EntProtoId TechnologyDiskCircuitboard = "TechDiskComputerCircuitboard";
    private static readonly EntProtoId TechnologyDiskObjective = "TechnologyDiskStealCollectionObjective";
    private static readonly ProtoId<CargoBountyPrototype> TechnologyDiskBounty = "BountyTechDisk";
    private static readonly ProtoId<TechnologyPrototype> AlternativeResearch = "AlternativeResearch";
    private static readonly ProtoId<ConstructionPrototype> ImprovisedServerConstruction =
        "SunriseImprovisedResearchServerCircuitboardConstruction";
    private static readonly ProtoId<ConstructionGraphPrototype> ImprovisedServerGraph =
        "SunriseImprovisedResearchServerCircuitboardGraph";
    private static readonly EntProtoId ImprovisedServerBoard =
        "SunriseImprovisedResearchAndDevelopmentServerMachineCircuitboard";
    private static readonly EntProtoId ImprovisedServer = "SunriseImprovisedResearchAndDevelopmentServer";
    private const string StandardServerBoard = "ResearchAndDevelopmentServerMachineCircuitboard";
    private static readonly ProtoId<TechnologyPrototype> AudioVisualCommunicationT3 =
        "AudioVisualCommunicationT3";
    private static readonly ProtoId<LatheRecipePackPrototype> ServiceBoards = "ServiceBoards";
    private static readonly ProtoId<CargoProductPrototype> ResearchServerCargoProduct = "ResearchServer";
    private static readonly ProtoId<TechnologyPrototype>[] ResearchExpansions =
    [
        "SunriseResearchDisciplineExpansion1",
        "SunriseResearchDisciplineExpansion2",
        "SunriseResearchDisciplineExpansion3",
        "SunriseResearchDisciplineExpansion4",
    ];

    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task EndgameTechnologyRequiresCompletedResearch()
    {
        var research = Server.System<ResearchSystem>();
        var endgame = SProtoMan.Index(ResearchGrant);

        await Server.WaitAssertion(() =>
        {
            var incomplete = new TechnologyDatabaseComponent
            {
                SupportedDisciplines = [CivilianServices],
                MainDiscipline = CivilianServices,
            };
            var completed = CreateCompletedDatabase(CivilianServices);

            Assert.Multiple(() =>
            {
                Assert.That(research.IsTechnologyAvailable(incomplete, endgame), Is.False);
                Assert.That(research.IsTechnologyAvailable(completed, endgame), Is.True);
            });
        });
    }

    [Test]
    public async Task EmptyExpansionSlotBlocksCompletion()
    {
        var research = Server.System<ResearchSystem>();

        await Server.WaitAssertion(() =>
        {
            var database = CreateCompletedDatabase(Experimental);
            database.ResearchDisciplineLimit = 2;

            Assert.That(research.HasCompletedResearch(database), Is.False);
        });
    }

    [Test]
    public async Task AdditionalTierThreeDisciplineUsesPurchasedSlot()
    {
        var research = Server.System<ResearchSystem>();
        var industrial = SProtoMan.Index(Industrial);
        var technology = SProtoMan.EnumeratePrototypes<TechnologyPrototype>()
            .First(tech => tech.Discipline == industrial.ID && tech.Tier >= industrial.LockoutTier);
        EntityUid databaseUid = default;

        await Server.WaitPost(() =>
        {
            databaseUid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var database = SEntMan.AddComponent<TechnologyDatabaseComponent>(databaseUid);
            var completed = CreateCompletedDatabase(Experimental, Industrial);
            database.MainDiscipline = completed.MainDiscipline;
            database.SupportedDisciplines = completed.SupportedDisciplines;
            database.UnlockedTechnologies = completed.UnlockedTechnologies;
            database.ResearchDisciplineLimit = 2;

            Assert.That(research.GetHighestDisciplineTier(database, industrial),
                Is.GreaterThanOrEqualTo(industrial.LockoutTier));

            research.TrySetMainDiscipline(technology, databaseUid, database);
        });

        await Server.WaitAssertion(() =>
        {
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);

            Assert.Multiple(() =>
            {
                Assert.That(database.AdditionalDisciplines, Does.Contain(industrial.ID));
                Assert.That(research.GetHighestDisciplineTier(database, industrial),
                    Is.GreaterThanOrEqualTo(industrial.LockoutTier));
            });
        });
    }

    [Test]
    public async Task NoMainDisciplineIsNotComplete()
    {
        var research = Server.System<ResearchSystem>();

        await Server.WaitAssertion(() =>
        {
            var database = CreateCompletedDatabase(Experimental);
            database.MainDiscipline = null;

            Assert.That(research.HasCompletedResearch(database), Is.False);
        });
    }

    [Test]
    public async Task TechnologyTierLimitBlocksWholeTechnologyButNotDiskRecipe()
    {
        var research = Server.System<ResearchSystem>();
        var tierThree = SProtoMan.EnumeratePrototypes<TechnologyPrototype>()
            .First(technology => technology.Tier == 3 &&
                                 !technology.Hidden &&
                                 !technology.RequiresResearchCompletion &&
                                 technology.RecipeUnlocks.Count > 0);
        var tierTwo = SProtoMan.EnumeratePrototypes<TechnologyPrototype>()
            .First(technology => technology.Discipline == tierThree.Discipline &&
                                 technology.Tier == 2 &&
                                 !technology.Hidden);
        EntityUid databaseUid = default;

        await Server.WaitPost(() =>
        {
            databaseUid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var database = SEntMan.AddComponent<TechnologyDatabaseComponent>(databaseUid);
            var completed = CreateCompletedDatabase(tierThree.Discipline);
            database.MainDiscipline = completed.MainDiscipline;
            database.SupportedDisciplines = completed.SupportedDisciplines;
            database.UnlockedTechnologies = completed.UnlockedTechnologies;
            database.UnlockedTechnologies.Remove(tierThree.ID);
            database.UnlockedTechnologies.Remove(tierTwo.ID);
            database.MaximumTechnologyTier = 2;

            Assert.Multiple(() =>
            {
                Assert.That(research.IsTechnologyAvailable(database, tierTwo), Is.True);
                Assert.That(research.IsTechnologyAvailable(database, tierThree), Is.False);
            });

            research.AddTechnology(databaseUid, tierThree, database);
            research.AddLatheRecipe(databaseUid, tierThree.RecipeUnlocks[0], database);
        });

        await Server.WaitAssertion(() =>
        {
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);

            Assert.Multiple(() =>
            {
                Assert.That(database.UnlockedTechnologies, Does.Not.Contain(tierThree.ID));
                Assert.That(database.UnlockedRecipes, Does.Contain(tierThree.RecipeUnlocks[0]));
            });
        });
    }

    [Test]
    public async Task ImprovisedServerLocksToFirstTierTwoDiscipline()
    {
        var research = Server.System<ResearchSystem>();
        var technologies = SProtoMan.EnumeratePrototypes<TechnologyPrototype>().ToList();
        var first = technologies.First(technology => technology.Discipline == Experimental && technology.Tier == 2 &&
                                                     !technology.Hidden && technology.RecipeUnlocks.Count > 0);
        var second = technologies.First(technology => technology.Discipline == Industrial && technology.Tier == 2 &&
                                                      !technology.Hidden && technology.RecipeUnlocks.Count > 0);
        var basic = technologies.First(technology => technology.Discipline == Industrial && technology.Tier == 1 &&
                                                     !technology.Hidden && technology.TechnologyPrerequisites.Count == 0);

        await Server.WaitPost(() =>
        {
            var server = SEntMan.SpawnEntity("SunriseImprovisedResearchAndDevelopmentServer", MapCoordinates.Nullspace);
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(server);
            var completed = CreateCompletedDatabase(Experimental, Industrial);
            database.UnlockedTechnologies = completed.UnlockedTechnologies;
            database.UnlockedTechnologies.Remove(first.ID);
            database.UnlockedTechnologies.Remove(second.ID);
            database.UnlockedTechnologies.Remove(basic.ID);

            research.AddTechnology(server, first, database);
            Assert.That(database.MainDiscipline, Is.EqualTo(Experimental));
            Assert.That(research.IsTechnologyAvailable(database, second), Is.False);
            Assert.That(research.IsTechnologyAvailable(database, basic), Is.True);
            research.AddTechnology(server, second, database);
            Assert.That(database.UnlockedTechnologies, Does.Not.Contain(second.ID));
            research.AddLatheRecipe(server, second.RecipeUnlocks[0], database);
            Assert.That(database.UnlockedRecipes, Does.Contain(second.RecipeUnlocks[0]));

            var client = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var clientDatabase = SEntMan.AddComponent<TechnologyDatabaseComponent>(client);
            research.Sync(client, server, clientDatabase, database);
            Assert.That(clientDatabase.DisciplineLockoutTier, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ImprovisedServerConstructionUsesRequiredPartsAndToolSequence()
    {
        var construction = SProtoMan.Index(ImprovisedServerConstruction);
        var graph = SProtoMan.Index(ImprovisedServerGraph);
        var finalBoard = SProtoMan.Index(ImprovisedServerBoard);
        var server = SProtoMan.Index(ImprovisedServer);
        EntityUid telecomBoard = default;
        EntityUid otherBoard = default;
        EntityUid scienceKey = default;
        EntityUid otherKey = default;

        await Server.WaitPost(() =>
        {
            telecomBoard = SEntMan.SpawnEntity("TelecomServerCircuitboard", MapCoordinates.Nullspace);
            otherBoard = SEntMan.SpawnEntity("ResearchAndDevelopmentServerMachineCircuitboard", MapCoordinates.Nullspace);
            scienceKey = SEntMan.SpawnEntity("EncryptionKeyScience", MapCoordinates.Nullspace);
            otherKey = SEntMan.SpawnEntity("EncryptionKeyMedical", MapCoordinates.Nullspace);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(construction.Graph, Is.EqualTo(ImprovisedServerGraph));
            Assert.That(construction.StartNode, Is.EqualTo("start"));
            Assert.That(construction.TargetNode, Is.EqualTo("complete"));
            Assert.That(graph.PathId("start", "complete"), Is.EqualTo(new[]
            {
                "assembled",
                "welded",
                "casingOpen",
                "keyInserted",
                "secured",
                "complete",
            }));

            var initialSteps = graph.Edge("start", "assembled")!.Steps;
            Assert.That(initialSteps, Has.Count.EqualTo(3));
            var sourceBoard = initialSteps[0] as TagConstructionGraphStep;
            var pai = initialSteps[1] as ComponentConstructionGraphStep;
            var cable = initialSteps[2] as MaterialConstructionGraphStep;
            Assert.That(sourceBoard, Is.Not.Null);
            Assert.That(sourceBoard!.EntityValid(telecomBoard, SEntMan, SEntMan.ComponentFactory), Is.True);
            Assert.That(sourceBoard.EntityValid(otherBoard, SEntMan, SEntMan.ComponentFactory), Is.False);
            Assert.That(pai, Is.Not.Null);
            Assert.That(pai!.Component, Is.EqualTo("PAI"));
            Assert.That(cable, Is.Not.Null);
            Assert.That(cable!.MaterialPrototypeId, Is.EqualTo("CableHV"));
            Assert.That(cable.Amount, Is.EqualTo(10));

            var welding = graph.Edge("assembled", "welded")!.Steps.Single() as ToolConstructionGraphStep;
            var firstScrewing = graph.Edge("welded", "casingOpen")!.Steps.Single() as ToolConstructionGraphStep;
            var key = graph.Edge("casingOpen", "keyInserted")!.Steps.Single() as TagConstructionGraphStep;
            var secondScrewing = graph.Edge("keyInserted", "secured")!.Steps.Single() as ToolConstructionGraphStep;
            var pulsing = graph.Edge("secured", "complete")!.Steps.Single() as ToolConstructionGraphStep;
            Assert.That(welding!.Tool, Is.EqualTo((ProtoId<ToolQualityPrototype>) "Welding"));
            Assert.That(firstScrewing!.Tool, Is.EqualTo((ProtoId<ToolQualityPrototype>) "Screwing"));
            Assert.That(key, Is.Not.Null);
            Assert.That(key!.EntityValid(scienceKey, SEntMan, SEntMan.ComponentFactory), Is.True);
            Assert.That(key.EntityValid(otherKey, SEntMan, SEntMan.ComponentFactory), Is.False);
            Assert.That(secondScrewing!.Tool, Is.EqualTo((ProtoId<ToolQualityPrototype>) "Screwing"));
            Assert.That(pulsing!.Tool, Is.EqualTo((ProtoId<ToolQualityPrototype>) "Pulsing"));
            Assert.That(pulsing.DoAfter, Is.EqualTo(3));

            Assert.That(finalBoard.TryComp<MachineBoardComponent>(out var machineBoard, SEntMan.ComponentFactory),
                Is.True);
            Assert.That(machineBoard!.Prototype, Is.EqualTo(ImprovisedServer));
            Assert.That(machineBoard.StackRequirements[(ProtoId<StackPrototype>) "Plasma"], Is.EqualTo(5));
            Assert.That(server.TryComp<TechnologyDatabaseComponent>(out var database, SEntMan.ComponentFactory),
                Is.True);
            Assert.That(database!.MaximumTechnologyTier, Is.EqualTo(2));
            Assert.That(database.DisciplineLockoutTier, Is.EqualTo(2));
            Assert.That(database.SupportedDisciplines, Has.Count.EqualTo(5));
        });
    }

    [Test]
    public void StandardResearchServerIsCargoOnly()
    {
        var technology = SProtoMan.Index(AudioVisualCommunicationT3);
        var serviceBoards = SProtoMan.Index(ServiceBoards);
        var cargoProduct = SProtoMan.Index(ResearchServerCargoProduct);

        Assert.Multiple(() =>
        {
            Assert.That(technology.RecipeUnlocks, Does.Not.Contain(StandardServerBoard));
            Assert.That(SProtoMan.HasIndex<LatheRecipePrototype>(StandardServerBoard), Is.False);
            Assert.That(serviceBoards.Recipes, Does.Not.Contain(StandardServerBoard));
            Assert.That(cargoProduct.Cost, Is.EqualTo(75_000));
        });
    }

    [Test]
    public void ExpansionValuesAndTechnologyDiskContentArePreserved()
    {
        var grant = SProtoMan.Index(ResearchGrant);
        var expansions = ResearchExpansions.Select(SProtoMan.Index).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(grant.Cost, Is.EqualTo(10_000));
            Assert.That(grant.Repeatable, Is.True);
            Assert.That(((ResearchCargoGrantEvent) grant.GenericUnlocks.Single().PurchaseEvent!).Amount,
                Is.EqualTo(1_000));
            Assert.That(expansions.Select(expansion => expansion.Cost),
                Is.EqualTo(new[] { 100_000, 250_000, 400_000, 600_000 }));
            Assert.That(expansions[1].TechnologyPrerequisites, Does.Contain(expansions[0].ID));
            Assert.That(expansions[2].TechnologyPrerequisites, Does.Contain(expansions[1].ID));
            Assert.That(expansions[3].TechnologyPrerequisites, Does.Contain(expansions[2].ID));

            Assert.That(SProtoMan.HasIndex<EntityPrototype>(TechnologyDiskTerminal), Is.True);
            Assert.That(SProtoMan.HasIndex<EntityPrototype>(TechnologyDiskCircuitboard), Is.True);
            Assert.That(SProtoMan.HasIndex<EntityPrototype>(TechnologyDiskObjective), Is.True);
            Assert.That(SProtoMan.HasIndex<CargoBountyPrototype>(TechnologyDiskBounty), Is.True);
            Assert.That(SProtoMan.HasIndex<TechnologyPrototype>(AlternativeResearch), Is.True);
        });
    }

    [Test]
    public async Task SelectingDisciplineRaisesNotificationEventOnce()
    {
        var research = Server.System<ResearchSystem>();
        var listener = Server.System<DisciplineSelectedListenerSystem>();
        var industrial = SProtoMan.Index(Industrial);
        var technology = SProtoMan.EnumeratePrototypes<TechnologyPrototype>()
            .First(tech => tech.Discipline == industrial.ID && tech.Tier >= industrial.LockoutTier);
        EntityUid databaseUid = default;

        await Server.WaitPost(() =>
        {
            databaseUid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var database = SEntMan.AddComponent<TechnologyDatabaseComponent>(databaseUid);
            SEntMan.AddComponent<TestListenerComponent>(databaseUid);
            database.MainDiscipline = Experimental;
            database.ResearchDisciplineLimit = 2;

            research.TrySetMainDiscipline(technology, databaseUid, database);
            research.TrySetMainDiscipline(technology, databaseUid, database);
        });

        await Server.WaitAssertion(() =>
        {
            var selected = listener.GetEvents(databaseUid).Single();
            Assert.That(selected.Discipline, Is.EqualTo(Industrial));
        });
    }

    [Test]
    public void MessengerNotificationLocalizationExists()
    {
        var localization = Server.Resolve<ILocalizationManager>();

        Assert.Multiple(() =>
        {
            Assert.That(localization.HasString("research-messenger-discipline-selected"), Is.True);
            Assert.That(localization.HasString("research-messenger-grant-received"), Is.True);
        });
    }

    private TechnologyDatabaseComponent CreateCompletedDatabase(
        params ProtoId<TechDisciplinePrototype>[] disciplines)
    {
        var database = new TechnologyDatabaseComponent
        {
            SupportedDisciplines = disciplines.ToList(),
            MainDiscipline = disciplines[0],
        };

        foreach (var technology in SProtoMan.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (technology.Hidden ||
                technology.RequiresResearchCompletion ||
                !database.SupportedDisciplines.Contains(technology.Discipline))
            {
                continue;
            }

            database.UnlockedTechnologies.Add(technology.ID);
        }

        return database;
    }
}
#pragma warning restore RA0002

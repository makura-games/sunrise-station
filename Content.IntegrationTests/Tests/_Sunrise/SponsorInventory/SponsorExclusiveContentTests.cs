using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Holiday.Christmas;
using Content.Server._Sunrise.Research.Artifact.Effects.RandomTransformation;
using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Tests._Sunrise.SponsorInventory;

/// <summary>
/// Проверяет разделение коллекций: обычные вещи доступны в игре, эксклюзивные защищены от бесплатной выдачи.
/// </summary>
[TestFixture, NonParallelizable]
public sealed class SponsorExclusiveContentTests : GameTest
{
    private const string CategoryId = "SponsorExclusive";
    private const string ClassicCategoryId = "SponsorClassic";
    private const string OrdinaryItemId = "ClothingUniformJumpsuitColorGrey";
    private const string ExclusiveId = "SunriseTestSponsorExclusiveItem";
    private const string ChildId = "SunriseTestSponsorExclusiveChild";
    private const string ClassicChildId = "SunriseTestSponsorClassicChild";
    private static readonly EntProtoId TransformationEffect = "SunriseEffectRandomTransformation";

    [SidedDependency(Side.Server)] private readonly IResourceManager _resources = null!;
    [SidedDependency(Side.Server)] private readonly SharedChameleonClothingSystem _chameleon = null!;
    [SidedDependency(Side.Server)] private readonly RandomGiftSystem _gifts = null!;
    [SidedDependency(Side.Server)] private readonly ArtifactRandomTransformationSystem _transformation = null!;

    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: SunriseTestSponsorExclusiveItem
          parent: ClothingUniformJumpsuitColorGrey
          categories: [SponsorExclusive]

        - type: entity
          id: SunriseTestSponsorExclusiveChild
          parent: SunriseTestSponsorExclusiveItem

        - type: entity
          id: SunriseTestSponsorClassicChild
          parent: ClothingCostumeBunnySuit
        """;

    [Test]
    public async Task SelectedItemsAreClassicAndCategoriesStaySeparate()
    {
        string[] selected =
        [
            "ClothingOuterDiscoAssBlazer", "ClothingUniformJumpsuitAerostatic", "ClothingHeadHatDeerHorns",
            "ClothingCostumeNaota", "ClothingUniformDressRed", "ClothingUniformSkirtTurtle",
            "ClothingUniformSchoolGakuranBlack", "ClothingNeckKailoRen", "UniformSchoolgirlDusk",
            "ClothingUniformTShirtKhakiPants", "ClothingCostumeBunnySuit", "UniformSchoolgirlRed",
            "UniformSchoolgirlBlue", "UniformSchoolgirlCyan", "UniformSchoolgirlGreen",
            "UniformSchoolgirlOrange", "UniformSchoolgirlPurple", "UniformSchoolgirlBlazerTan",
            "ClothingUniformJumpsuitMantis", "ClothingUniformSkirtMantis", "ClothingUniformUndershirt",
            "ClothingUniformJumpsuitSecSummer", "UniformSecurityShortsRed", "ClothingUniformJumpsuitRecruitNT",
            "ClothingUniformJumpsuitRepairmanNT", "ClothingUniformJumpskirtColorBrown",
            "ClothingUniformJumpsuitColorLightBrown", "ClothingCostumeArcDress", "UniformGeisha", "UniformMaid",
            "ClothingUniformRat", "ClothingUniformKendoHakama", "ClothingUniformMartialGi",
            "ClothingKimonoBlue", "ClothingKimonoPink", "ClothingKimonoPurple", ClassicChildId,
        ];
        await Server.WaitAssertion(() =>
        {
            Assert.That(SProtoMan.Index<EntityCategoryPrototype>(CategoryId).Inheritable, Is.True);
            Assert.That(SProtoMan.Index<EntityCategoryPrototype>(CategoryId).HideSpawnMenu, Is.True);
            Assert.That(SProtoMan.Index<EntityCategoryPrototype>(ClassicCategoryId).Inheritable, Is.True);
            Assert.That(SProtoMan.Index<EntityCategoryPrototype>(ClassicCategoryId).HideSpawnMenu, Is.False);
            Assert.Multiple(() =>
            {
                foreach (var id in selected)
                {
                    var prototype = SProtoMan.Index<EntityPrototype>(id);
                    Assert.That(IsClassic(prototype), Is.True, $"Нет обычной спонсорской категории у {id}");
                    Assert.That(IsExclusive(prototype), Is.False, $"Обычная вещь {id} стала эксклюзивной");
                    Assert.That(prototype.HideSpawnMenu, Is.False, $"Обычная вещь {id} скрыта из меню спавна");
                }

                foreach (var prototype in ExclusivePrototypes())
                {
                    Assert.That(IsClassic(prototype), Is.False, $"У {prototype.ID} обе спонсорские категории");
                    Assert.That(prototype.HideSpawnMenu, Is.True, $"Эксклюзивная вещь {prototype.ID} видна в меню спавна");
                }

                Assert.That(IsExclusive(SProtoMan.Index<EntityPrototype>(ExclusiveId)), Is.True);
                Assert.That(IsExclusive(SProtoMan.Index<EntityPrototype>(ChildId)), Is.True);

                string[] excluded =
                [
                    "ClothingHeadCapCentcomNaval", "ClothingUniformJumpsuitCentcomAdmiral",
                    "ClothingUniformJumpsuitCentcomOfficerBlack", "ClothingShoesCentcomBlack",
                ];
                foreach (var id in excluded)
                {
                    var prototype = SProtoMan.Index<EntityPrototype>(id);
                    Assert.That(IsClassic(prototype) || IsExclusive(prototype), Is.False, $"Исключённая вещь {id} помечена спонсорской");
                }
            });
        });
    }

    [Test]
    public async Task ClassicItemsRemainAvailableThroughDynamicSources()
    {
        await Server.WaitAssertion(() =>
        {
            var effect = SProtoMan.Index(TransformationEffect);
            Assert.That(effect.TryComp(out ArtifactRandomTransformationComponent config, SEntMan.ComponentFactory), Is.True);
            var field = typeof(RandomGiftSystem).GetField("_possibleGiftsSafe", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var gifts = (List<string>) field!.GetValue(_gifts)!;

            Assert.Multiple(() =>
            {
                foreach (var prototype in SProtoMan.EnumeratePrototypes<EntityPrototype>().Where(IsClassic))
                {
                    Assert.That(gifts, Does.Contain(prototype.ID), $"Обычная вещь {prototype.ID} исключена из подарков");
                    Assert.That(_chameleon.IsValidTarget(prototype), Is.True, $"Хамелеон не копирует обычную вещь {prototype.ID}");
                    Assert.That(_transformation.CanTransformInto((EntityUid.Invalid, config!), prototype), Is.True,
                        $"Артефакт не создаёт обычную вещь {prototype.ID}");
                }
            });
        });
    }

    [Test]
    public async Task HiddenExclusivePrototypeCanStillBeSpawnedById()
    {
        var item = EntityUid.Invalid;
        try
        {
            await Server.WaitPost(() => item = SEntMan.SpawnEntity(ExclusiveId, MapCoordinates.Nullspace));
            await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(item), Is.True));
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(item))
                    SEntMan.DeleteEntity(item);
            });
        }
    }

    [Test]
    public async Task ChameleonRejectsExclusiveItems()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(_chameleon.IsValidTarget(SProtoMan.Index<EntityPrototype>(OrdinaryItemId)), Is.True);
            Assert.Multiple(() =>
            {
                foreach (var prototype in ExclusivePrototypes())
                    Assert.That(_chameleon.IsValidTarget(prototype), Is.False, $"Хамелеон копирует {prototype.ID}");
            });
        });
    }

    [TestCase("_possibleGiftsSafe")]
    [TestCase("_possibleGiftsUnsafe")]
    public async Task GiftPoolsRejectExclusiveItems(string fieldName)
    {
        await Server.WaitAssertion(() =>
        {
            // Проверяем реальные пулы целиком; случайная выдача не гарантировала бы обнаружения обхода.
            var field = typeof(RandomGiftSystem).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Пул подарков {fieldName} переименован; обновите проверку");
            var pool = (List<string>) field!.GetValue(_gifts)!;
            Assert.That(pool, Does.Contain(OrdinaryItemId));
            Assert.Multiple(() =>
            {
                foreach (var prototype in ExclusivePrototypes())
                    Assert.That(pool, Does.Not.Contain(prototype.ID), $"Подарки {fieldName} выдают {prototype.ID}");
            });
        });
    }

    [Test]
    public async Task ArtifactRejectsExclusiveItems()
    {
        await Server.WaitAssertion(() =>
        {
            var effect = SProtoMan.Index(TransformationEffect);
            Assert.That(effect.TryComp(out ArtifactRandomTransformationComponent config, SEntMan.ComponentFactory), Is.True);
            Assert.That(_transformation.CanTransformInto((EntityUid.Invalid, config!), SProtoMan.Index<EntityPrototype>(OrdinaryItemId)), Is.True);
            Assert.Multiple(() =>
            {
                foreach (var prototype in ExclusivePrototypes())
                    Assert.That(_transformation.CanTransformInto((EntityUid.Invalid, config!), prototype), Is.False,
                        $"Артефакт создаёт {prototype.ID}");
            });
        });
    }

    [Test]
    public Task MapsHaveNoExclusiveReferences() => CheckReferences(new ResPath("/Maps"), false);

    [Test]
    public Task OrdinarySourcesHaveNoExclusiveReferences() => CheckReferences(new ResPath("/Prototypes"), true);

    private async Task CheckReferences(ResPath directory, bool prototypeFile)
    {
        HashSet<string> exclusiveIds = null!;
        await Server.WaitAssertion(() =>
        {
            exclusiveIds = ExclusivePrototypes().Select(p => p.ID).ToHashSet();
            var migrations = new BeforeEntityReadEvent();
            SEntMan.EventBus.RaiseEvent(EventSource.Local, migrations);
            SponsorExclusiveReferences.IncludeMigratedIds(exclusiveIds, migrations.RenamedPrototypes);
        });
        var files = _resources.ContentFindFiles(directory)
            .Where(p => p.ToString().EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                        p.ToString().EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.ToString()).ToArray();
        Assert.That(files, Is.Not.Empty, $"Не найдены ресурсы {directory}");
        var violations = new List<string>();
        foreach (var file in files)
        {
            using var reader = _resources.ContentFileReadText(file);
            var stream = new YamlStream();
            stream.Load(reader);
            foreach (var document in stream.Documents)
            {
                foreach (var reference in SponsorExclusiveReferences.Find(document.RootNode, exclusiveIds, prototypeFile))
                    violations.Add($"{file}: {reference}");
            }
        }

        Assert.That(violations, Is.Empty, string.Join('\n', violations));
    }

    private EntityPrototype[] ExclusivePrototypes()
    {
        var prototypes = SProtoMan.EnumeratePrototypes<EntityPrototype>().Where(IsExclusive).ToArray();
        Assert.That(prototypes, Is.Not.Empty, "Не загружены эксклюзивные спонсорские предметы");
        return prototypes;
    }

    private static bool IsExclusive(EntityPrototype prototype) => prototype.Categories.Any(c => c.ID == CategoryId);

    private static bool IsClassic(EntityPrototype prototype) => prototype.Categories.Any(c => c.ID == ClassicCategoryId);
}

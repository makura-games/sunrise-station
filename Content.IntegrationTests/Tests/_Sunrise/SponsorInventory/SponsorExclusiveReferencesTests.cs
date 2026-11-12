using System.Collections.Generic;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Tests._Sunrise.SponsorInventory;

/// <summary>
/// Контрольные нарушения подтверждают, что проверка замечает каждый способ записи ссылки на предмет.
/// </summary>
[TestFixture]
public sealed class SponsorExclusiveReferencesTests
{
    private static readonly HashSet<string> ExclusiveIds = ["ClothingUniformDressRed"];

    [TestCase("entities:\n- proto: ClothingUniformDressRed")]
    [TestCase("equipment: { jumpsuit: ClothingUniformDressRed }")]
    [TestCase("storage: { back: [ClothingUniformDressRed] }")]
    [TestCase("products: { ClothingUniformDressRed: 1 }")]
    [TestCase("table: !type:AllSelector\n  children:\n  - id: ClothingUniformDressRed")]
    [TestCase("components:\n- type: RandomSpawner\n  rarePrototypes: [ClothingUniformDressRed]")]
    public void RejectsForbiddenReference(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        Assert.That(SponsorExclusiveReferences.Find(stream.Documents[0].RootNode, ExclusiveIds, false), Is.Not.Empty);
    }

    [Test]
    public void AllowsDeclarationInheritanceAndOrdinaryItems()
    {
        const string yaml = """
            - type: entity
              id: ClothingUniformDressRed
            - type: entity
              id: SunriseTestExclusiveChild
              parent: ClothingUniformDressRed
            - type: startingGear
              id: ClothingUniformDressRed
              equipment:
                jumpsuit: ClothingUniformJumpsuitColorGrey
            """;
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        Assert.That(SponsorExclusiveReferences.Find(stream.Documents[0].RootNode, ExclusiveIds, true), Is.Empty);
    }

    [Test]
    public void RecognizesMigratedIds()
    {
        var ids = new HashSet<string>(ExclusiveIds);
        var renames = new Dictionary<string, string>
        {
            ["SunriseTestOldDress"] = "SunriseTestIntermediateDress",
            ["SunriseTestIntermediateDress"] = "ClothingUniformDressRed",
            ["SunriseTestOrdinaryAlias"] = "ClothingUniformJumpsuitColorGrey",
        };
        SponsorExclusiveReferences.IncludeMigratedIds(ids, renames);
        Assert.Multiple(() =>
        {
            Assert.That(ids, Does.Contain("SunriseTestOldDress"));
            Assert.That(ids, Does.Contain("SunriseTestIntermediateDress"));
            Assert.That(ids, Does.Not.Contain("SunriseTestOrdinaryAlias"));
        });
    }
}

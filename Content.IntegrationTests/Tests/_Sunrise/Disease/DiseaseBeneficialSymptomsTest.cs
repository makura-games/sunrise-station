using Content.IntegrationTests.Fixtures;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Disease;

/// <summary>
/// Полезные симптомы выводятся только под микроскопом и не должны попадать в природные болезни.
/// </summary>
public sealed class DiseaseBeneficialSymptomsTest : GameTest
{
    /// <summary>
    /// Ни одна болезнь не перечисляет полезный симптом в пуле стадии.
    /// </summary>
    [Test]
    public async Task DiseasesHaveNoBeneficialSymptoms()
    {
        var server = Pair.Server;
        var protoMan = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var disease in protoMan.EnumeratePrototypes<DiseasePrototype>())
                {
                    foreach (var stage in disease.Stages)
                    {
                        foreach (var entry in stage.SymptomPool)
                        {
                            Assert.That(protoMan.Index(entry.Symptom).Beneficial, Is.False,
                                $"Disease \"{disease.ID}\" lists beneficial symptom \"{entry.Symptom}\" in a stage pool");
                        }
                    }
                }
            });
        });
    }

    /// <summary>
    /// Заготовки держат полезные симптомы только в отдельном списке, а в нём — только полезные.
    /// </summary>
    [Test]
    public async Task BlankSamplesKeepBeneficialSymptomsSeparate()
    {
        var server = Pair.Server;
        var protoMan = server.ProtoMan;
        var compFactory = server.EntMan.ComponentFactory;

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract || !proto.TryGetComponent<DiseaseBlankSampleComponent>(out var blank, compFactory))
                        continue;

                    foreach (var entry in blank.Symptoms)
                    {
                        Assert.That(protoMan.Index(entry.Symptom).Beneficial, Is.False,
                            $"Blank sample \"{proto.ID}\" lists beneficial symptom \"{entry.Symptom}\" in its common pool");
                    }

                    foreach (var entry in blank.BeneficialSymptoms)
                    {
                        Assert.That(protoMan.Index(entry.Symptom).Beneficial, Is.True,
                            $"Blank sample \"{proto.ID}\" lists harmful symptom \"{entry.Symptom}\" as beneficial");
                    }
                }
            });
        });
    }
}

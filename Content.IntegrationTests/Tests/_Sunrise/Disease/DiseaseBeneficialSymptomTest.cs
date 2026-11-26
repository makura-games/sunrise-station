#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Disease;

/// <summary>
/// Следит за тем, чтобы полезные симптомы не попадали в пулы болезней.
/// </summary>
/// <remarks>
/// Полезный симптом задуман как награда за селекцию: вирусолог собирает его в штамм под
/// микроскопом вручную. Если такой симптом окажется в пуле обычной болезни, его можно будет
/// получить, просто заразившись, и смысл селекции пропадёт. Раньше это отсекалось в рантайме,
/// при сборке штамма, но там ошибка прототипа молча исчезала; теперь она падает тестом.
/// </remarks>
public sealed class DiseaseBeneficialSymptomTest : GameTest
{
    /// <summary>
    /// Болезни, которым полезные симптомы разрешены намеренно.
    /// </summary>
    /// <remarks>
    /// Добавляя сюда болезнь, напишите рядом, почему ей это можно: без причины запись
    /// превращается в способ обойти проверку.
    /// </remarks>
    private static readonly HashSet<string> Allowed = new()
    {
    };

    [Test]
    [Description("checks that no disease prototype offers a beneficial symptom in its stage pools")]
    public async Task DiseasePoolsHaveNoBeneficialSymptoms()
    {
        var server = Pair.Server;
        var protoMan = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var disease in protoMan.EnumeratePrototypes<DiseasePrototype>())
                {
                    if (Allowed.Contains(disease.ID))
                        continue;

                    // Запасной симптом воздушного пути подставляется в обход пулов стадий,
                    // поэтому его проверяем отдельно.
                    if (protoMan.TryIndex(disease.DropletFallbackSymptom, out var fallback))
                    {
                        Assert.That(fallback.Beneficial, Is.False,
                            $"Болезнь \"{disease.ID}\" берёт запасным симптомом воздушного пути " +
                            $"полезный \"{disease.DropletFallbackSymptom}\". Такой симптом достанется " +
                            $"носителю в обход селекции.");
                    }

                    for (var i = 0; i < disease.Stages.Count; i++)
                    {
                        foreach (var entry in disease.Stages[i].SymptomPool)
                        {
                            // Несуществующие идентификаторы ловит линтер прототипов, здесь нас
                            // интересует только природа найденного симптома.
                            if (!protoMan.TryIndex(entry.Symptom, out var symptom))
                                continue;

                            Assert.That(symptom.Beneficial, Is.False,
                                $"Болезнь \"{disease.ID}\" предлагает полезный симптом \"{entry.Symptom}\" " +
                                $"на стадии {i}. Полезные симптомы выводятся только под микроскопом. " +
                                $"Если это сделано намеренно, добавьте болезнь в {nameof(Allowed)} " +
                                $"в {nameof(DiseaseBeneficialSymptomTest)} и объясните причину.");
                        }
                    }
                }
            });
        });
    }
}

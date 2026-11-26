using System.Numerics;
using Content.Server._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Считает силу болезни и подбирает по ней профиль сложности исследования.
/// </summary>
/// <remarks>
/// Сила складывается из заразности и тяжести симптомов, то есть из того, чем болезнь опасна для станции.
/// Чем она выше, тем больше пробных партий у вакцины и тем больше их свойств закрыто до расшифровки.
/// </remarks>
public sealed partial class DiseaseStrengthSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;

    /// <summary>
    /// Во сколько шанс контактной передачи за стадию весит в заразности.
    /// </summary>
    private const float ContactWeight = 10f;

    /// <summary>
    /// Во сколько капельная передача весит в заразности.
    /// </summary>
    private const float DropletWeight = 2f;

    /// <summary>
    /// Вес, с которым в силу входит суммарная тяжесть всех симптомов штамма.
    /// </summary>
    /// <remarks>
    /// Стадии дают среднюю тяжесть, поэтому сами по себе они считают штамм с десятком симптомов
    /// так же, как штамм с двумя. Широту набора добавляет отдельное слагаемое, а вес у него меньше
    /// единицы, потому что часть той же тяжести уже посчитана в среднем по стадиям.
    /// </remarks>
    private const float SymptomWeight = 0.75f;

    /// <summary>
    /// Возвращает силу болезни: заразность плюс тяжесть симптомов.
    /// </summary>
    /// <remarks>
    /// Считается один раз и ложится на сам штамм, в <see cref="DiseaseDefinitionComponent.Strength"/>:
    /// расчёт идёт по неизменяемым после сборки данным, так что одного раза хватает на весь раунд.
    /// </remarks>
    public int GetStrength(EntityUid disease)
    {
        if (!_registry.TryGetData(disease, out var definition))
            return 0;

        return definition.Strength ??= Compute(disease, definition);
    }

    /// <summary>
    /// Подбирает профиль сложности по силе болезни.
    /// </summary>
    /// <returns>Null, если профилей в прототипах нет вообще.</returns>
    public DiseaseDifficultyPrototype? GetDifficulty(EntityUid disease)
    {
        var strength = GetStrength(disease);
        DiseaseDifficultyPrototype? matched = null;
        DiseaseDifficultyPrototype? softest = null;

        foreach (var profile in _proto.EnumeratePrototypes<DiseaseDifficultyPrototype>())
        {
            if (strength >= profile.MinStrength && (matched == null || profile.MinStrength > matched.MinStrength))
                matched = profile;

            if (softest == null || profile.MinStrength < softest.MinStrength)
                softest = profile;
        }

        // Профиля с нулевым порогом может не оказаться: берём самый мягкий из существующих.
        return matched ?? softest;
    }

    /// <summary>
    /// Заразность: пути передачи, контактная передача по стадиям и капельный выброс симптомов.
    /// Тяжесть: средняя тяжесть симптомов пула, взвешенная по их весам и числу симптомов на стадии.
    /// </summary>
    private int Compute(EntityUid disease, DiseaseDefinitionComponent proto)
    {
        var contagion = (float)BitOperations.PopCount((uint)proto.Routes);
        var severity = 0f;

        for (var i = 0; i < proto.Stages.Count; i++)
        {
            var stage = proto.Stages[i];
            if ((proto.Routes & DiseaseTransmissionRoute.Contact) != 0)
                contagion += stage.ContactSpreadChance * ContactWeight;

            // Считаем по штамму раунда, а не по всему пулу архетипа: болезнь опасна ровно настолько,
            // насколько опасны выпавшие ей симптомы.
            var pool = _registry.GetStagePool(disease, i);

            var totalWeight = 0f;
            foreach (var entry in pool)
            {
                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0f)
                continue;

            var droplet = 0f;
            var stageSeverity = 0f;

            foreach (var entry in pool)
            {
                if (!_proto.TryIndex(entry.Symptom, out var symptom))
                    continue;

                // Доля симптома в пуле: насколько часто он вообще достаётся носителю.
                var share = entry.Weight / totalWeight;

                if (symptom.Spread is { } spread)
                    droplet += share * spread.Chance * spread.Range;

                stageSeverity += share * symptom.Severity;
            }

            if ((proto.Routes & DiseaseTransmissionRoute.Droplet) != 0)
                contagion += stage.SpreadMultiplier * droplet * DropletWeight;
            severity += stage.MaxSymptoms * stageSeverity;
        }

        return (int)MathF.Round(contagion + severity + GetSymptomPower(disease) * SymptomWeight);
    }

    /// <summary>
    /// Суммарная тяжесть всех симптомов, которые штамм способен выдать хотя бы на одной стадии.
    /// </summary>
    /// <remarks>
    /// Количество симптомов и их тяжесть здесь складываются: два лёгких симптома весят столько же,
    /// сколько один тяжёлый. Собранный под микроскопом штамм считается по тем же правилам,
    /// что и природный, поэтому широкая стадия честно поднимает его силу.
    /// </remarks>
    public float GetSymptomPower(EntityUid disease)
    {
        var power = 0f;
        foreach (var symptom in _registry.GetSymptoms(disease))
        {
            if (_proto.TryIndex(symptom, out var proto))
                power += proto.Severity;
        }

        return power;
    }
}

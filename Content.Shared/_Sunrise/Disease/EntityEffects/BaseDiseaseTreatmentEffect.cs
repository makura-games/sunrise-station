using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.EntityEffects;

/// <summary>
/// База для лечебных эффектов, действующих на часть болезней носителя.
/// Если фильтр не задан, эффект действует на все болезни носителя.
/// </summary>
/// <typeparam name="T">Конкретный тип эффекта.</typeparam>
public abstract partial class BaseDiseaseTreatmentEffect<T> : EntityEffectBase<T> where T : BaseDiseaseTreatmentEffect<T>
{
    /// <summary>
    /// Болезни, на которые действует эффект.
    /// </summary>
    [DataField]
    public List<ProtoId<DiseasePrototype>>? Diseases;

    /// <summary>
    /// Класс болезней, на который действует эффект. Игнорируется, если задано <see cref="Diseases"/>.
    /// </summary>
    [DataField]
    public DiseaseClassification? Classification;

    /// <summary>
    /// Собирает локализованное описание затрагиваемых болезней для текстов гайдбука.
    /// </summary>
    protected string GetTargetText(IPrototypeManager prototype)
    {
        if (Diseases is { Count: > 0 })
        {
            var names = new List<string>(Diseases.Count);
            foreach (var id in Diseases)
            {
                names.Add(prototype.TryIndex(id, out var disease)
                    ? Loc.GetString(disease.Name)
                    : id.Id);
            }

            return Loc.GetString("disease-effect-target-list", ("diseases", ContentLocalizationManager.FormatList(names)));
        }

        if (Classification is { } classification)
        {
            return Loc.GetString("disease-effect-target-class",
                ("class", Loc.GetString(DiseaseLocalization.GetClassificationLocId(classification))));
        }

        return Loc.GetString("disease-effect-target-all");
    }
}

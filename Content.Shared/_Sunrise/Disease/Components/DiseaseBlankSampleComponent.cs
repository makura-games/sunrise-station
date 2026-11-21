using Content.Shared._Sunrise.Disease.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Кандидаты симптомов и профили одноразовой инициализации заготовки при MapInit.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseBlankSampleComponent : Component
{
    /// <summary>
    /// Кандидаты и их веса для выбора пула без повторов.
    /// </summary>
    [DataField(required: true)]
    public List<DiseaseSymptomEntry> Symptoms = new();

    /// <summary>
    /// Полезные симптомы, один из которых изредка попадает на последнюю стадию заготовки.
    /// </summary>
    /// <remarks>
    /// Шанс задаёт <see cref="DiseaseBlankSampleProfile.BeneficialChance"/> выпавшего уровня.
    /// </remarks>
    [DataField]
    public List<DiseaseSymptomEntry> BeneficialSymptoms = new();

    /// <summary>
    /// Пустые симптомы-пробелы генома. С шансом <see cref="FillerChance"/> каждая стадия получает
    /// один из них сверх обычных вариантов, не занимая места других симптомов.
    /// </summary>
    /// <remarks>
    /// Развитый симптом пропадает из вариантов следующих стадий, поэтому пробелов несколько:
    /// иначе безвредной смогла бы стать только одна стадия генома.
    /// </remarks>
    [DataField]
    public List<DiseaseSymptomEntry> FillerSymptoms = new();

    /// <summary>
    /// Шанс, что стадия получит пустой симптом.
    /// </summary>
    [DataField]
    public float FillerChance = 0.25f;

    /// <summary>
    /// Уровни заготовки с весами выбора и диапазонами параметров.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<DiseaseBlankSampleQuality, DiseaseBlankSampleProfile> Profiles = new();

    /// <summary>
    /// Фиксированный уровень заготовки. Если не задан, уровень выбирается случайно по весам профилей.
    /// </summary>
    [DataField]
    public DiseaseBlankSampleQuality? Quality;

    /// <summary>
    /// Сколько вариантов симптомов стадия получает как минимум.
    /// </summary>
    /// <remarks>
    /// Меньше трёх нет смысла: один вариант станет целью партии, другой — колонией-конкурентом,
    /// и без запасного выбор цели сводится к «любой из двух». Эта же величина ограничивает длину
    /// вектора развития: на каждую стадию должно хватить вариантов из пула.
    /// </remarks>
    [DataField]
    public int MinStageOptions = 3;

    /// <summary>
    /// Потолок вариантов на стадию: с пятью выбор уже богатый, а список под микроскопом ещё читается.
    /// </summary>
    [DataField]
    public int MaxStageOptions = 5;

    /// <summary>
    /// Предел длины вектора развития, поверх диапазона профиля.
    /// </summary>
    /// <remarks>
    /// Вектор выпускают под архетип болезни, а у архетипов столько стадий и есть: собранное сверх
    /// этого повторяло бы форму последней стадии.
    /// </remarks>
    [DataField]
    public int MaxStages = 5;
}

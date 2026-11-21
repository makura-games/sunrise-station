using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Лабораторный образец известного штамма либо заготовка для синтеза симптомов.
/// Сам по себе не служит источником заражения.
/// </summary>
/// <remarks>
/// Образец с Disease используется для засева чашек Петри, заготовка с SymptomPool — для синтеза.
/// Клиенту уходят код на этикетке и признаки содержимого: по ним предсказываются
/// засев, укол и вставка в машины, а сам штамм остаётся на сервере.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseStrainSampleComponent : Component
{
    /// <summary>Пути передачи, развитые в этой заготовке.</summary>
    [DataField]
    public DiseaseTransmissionRoute Routes = DiseaseTransmissionRoute.Injection;

    /// <summary>
    /// Штамм в пробирке.
    /// </summary>
    [ViewVariables]
    public EntityUid? Disease;

    /// <summary>
    /// Есть ли в пробирке штамм. Сервер выставляет его вместе с <see cref="Disease"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool HasStrain;

    /// <summary>Это подготовленная заготовка, которую принимает селекционный микроскоп.</summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsBlank;

    /// <summary>Выведен ли штамм в лаборатории: только такой годится для сосудов для исследования.</summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsDesigned;

    /// <summary>
    /// Код штамма, которым подписана пробирка.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string StrainCode = string.Empty;

    /// <summary>
    /// Симптомы, из которых синтезатор развивает и усиливает образец.
    /// </summary>
    [DataField]
    public List<DiseaseSymptomEntry> SymptomPool = new();

    /// <summary>
    /// Варианты симптомов по стадиям развития: из них вирусолог выбирает цель партии под микроскопом.
    /// Поздние стадии набираются из более тяжёлых симптомов.
    /// </summary>
    [DataField]
    public List<DiseaseStagePool> StagePools = new();

    /// <summary>
    /// Собранный вектор развития: по пулу на каждую закрытую стадию.
    /// Он переносится в штамм один в один, поэтому болезнь развивается ровно так, как её собрали.
    /// </summary>
    /// <remarks>
    /// В пуле стадии лежит выбранный симптом и, если партия прошла грязно, примесь конкурента:
    /// чем хуже селекция, тем выше шанс, что носителю достанется не тот симптом.
    /// </remarks>
    [DataField]
    public List<DiseaseStagePool> Genome = new();

    /// <summary>
    /// Последняя закрытая стадия ещё открыта для усиления: её можно дополнить вторым симптомом
    /// или оставить как есть и перейти к следующей.
    /// </summary>
    [DataField]
    public bool StageOpen;

    /// <summary>
    /// Число выполненных операций развития симптомов.
    /// </summary>
    [DataField]
    public int Stage;

    /// <summary>
    /// Максимальное число стадий развития.
    /// </summary>
    [DataField]
    public int MaxStage;

    /// <summary>
    /// Устойчивость от нуля до единицы; расширяет допустимые диапазоны смеси.
    /// </summary>
    [DataField]
    public float Resistance;

    /// <summary>
    /// Сколько устойчивости добавляет каждая выигранная партия.
    /// </summary>
    /// <remarks>
    /// Культура крепчает по ходу селекции: чем длиннее геном, тем живучее штамм. Это и награда,
    /// и цена — устойчивый штамм дольше отдаёт очки в сосуде для исследования, но, перевалив порог,
    /// поднимает сложность следующих партий под микроскопом.
    /// </remarks>
    [DataField]
    public float ResistanceGain = 0.05f;

    /// <summary>
    /// Развитые симптомы без повторов.
    /// </summary>
    [DataField]
    public List<ProtoId<DiseaseSymptomPrototype>> Developed = new();

    /// <summary>
    /// Раскрыты ли максимальная стадия и списки симптомов.
    /// </summary>
    [DataField]
    public bool InfoRevealed;

    /// <summary>
    /// Сколько симптомов вирусолог может развить на одной стадии.
    /// </summary>
    /// <remarks>
    /// Шире этого стадия не растёт: иначе вектор развития превращается в кашу,
    /// а выведенный штамм проявляет все симптомы стадии сразу.
    /// </remarks>
    [DataField]
    public int MaxStageSymptoms = 3;

    /// <summary>
    /// Сколько усилений допускает заготовка.
    /// </summary>
    /// <remarks>
    /// Запас общий на весь геном, а не на стадию: широкий штамм должен стоить выбора,
    /// иначе выгодно усиливать каждую стадию подряд.
    /// </remarks>
    [DataField]
    public int MaxReinforcements = 2;
}

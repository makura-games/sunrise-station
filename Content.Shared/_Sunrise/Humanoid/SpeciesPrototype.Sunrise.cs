using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Humanoid.Prototypes;

public sealed partial class SpeciesPrototype
{
    /// <summary>
    /// Доступен ли вид только спонсорам.
    /// </summary>
    [DataField]
    public bool SponsorOnly { get; private set; }

    /// <summary>
    /// Прототипы телосложения, доступные этому виду.
    /// </summary>
    [DataField(required: true)]
    public List<string> BodyTypes { get; private set; } = default!;

    /// <summary>
    /// Набор мужских фамилий.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> MaleLastNames { get; private set; } = "NamesLast";

    /// <summary>
    /// Набор женских фамилий.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> FemaleLastNames { get; private set; } = "NamesLast";

    /// <summary>
    /// Минимальный множитель ширины персонажа.
    /// </summary>
    [DataField]
    public float MinWidth = 0.95f;

    /// <summary>
    /// Максимальный множитель ширины персонажа.
    /// </summary>
    [DataField]
    public float MaxWidth = 1.1f;

    /// <summary>
    /// Множитель ширины персонажа по умолчанию.
    /// </summary>
    [DataField]
    public float DefaultWidth = 1f;

    /// <summary>
    /// Минимальный множитель роста персонажа.
    /// </summary>
    [DataField]
    public float MinHeight = 0.9f;

    /// <summary>
    /// Максимальный множитель роста персонажа.
    /// </summary>
    [DataField]
    public float MaxHeight = 1.1f;

    /// <summary>
    /// Множитель роста персонажа по умолчанию.
    /// </summary>
    [DataField]
    public float DefaultHeight = 1f;

    /// <summary>
    /// Минимальный отображаемый рост в сантиметрах.
    /// </summary>
    [DataField]
    public float MinHeightCm = 150f;

    /// <summary>
    /// Максимальный отображаемый рост в сантиметрах.
    /// </summary>
    [DataField]
    public float MaxHeightCm = 200f;

    /// <summary>
    /// Вес в килограммах при стандартных росте и ширине.
    /// </summary>
    [DataField]
    public int StandardWeight = 75;

    /// <summary>
    /// Плотность, используемая для масштабирования веса вместе с размером персонажа.
    /// </summary>
    [DataField]
    public int StandardDensity = 120;

    /// <summary>
    /// Скрывается ли вид в записях станции по умолчанию.
    /// </summary>
    [DataField]
    public bool StationRecordsHidden;

    /// <summary>
    /// Спрайт предпросмотра вида.
    /// </summary>
    [DataField]
    public SpriteSpecifier Preview { get; private set; } =
        new SpriteSpecifier.Rsi(new ResPath("/Textures/Mobs/Species/Human/parts.rsi"), "full");

    /// <summary>
    /// Спрайт для сканера ягодиц Sunrise.
    /// </summary>
    [DataField]
    public SpriteSpecifier ButtScan =
        new SpriteSpecifier.Rsi(new ResPath("/Textures/_Sunrise/CopyMachine/butts_scans.rsi"), "human");
}

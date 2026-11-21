using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Реагент, который убивает заразу там, куда попал.
/// </summary>
/// <remarks>
/// Отдельная запись, а не поле компонента, потому что правило нужно обеим сторонам реакции:
/// очагу, который гасят, и человеку, с которого смывают заразу с перчаток. Перчатки отмывают
/// и тому, кто ни разу не болел, так что спрашивать настройку у его болезни нельзя.
/// Записей может быть несколько: любой реагент, объявленный здесь, работает дезинфектантом
/// со своим порогом, и для этого не нужно править ни одного прототипа предметов.
/// </remarks>
[Prototype]
public sealed partial class DiseaseDisinfectantPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Реагент, который считается дезинфектантом.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>
    /// Сколько реагента должно попасть на цель за одно касание.
    /// </summary>
    /// <remarks>
    /// Облако отдаёт раствор долями, так что одна капля очаг не гасит: нужно попасть струёй.
    /// </remarks>
    [DataField]
    public FixedPoint2 MinAmount = FixedPoint2.New(1);
}

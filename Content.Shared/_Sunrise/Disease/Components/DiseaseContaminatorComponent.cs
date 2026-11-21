using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Пробирка, культуру из которой можно незаметно подмешать в еду или питьё.
/// </summary>
/// <remarks>
/// Штамм заводится при появлении пробирки и лежит в её <see cref="DiseaseStrainSampleComponent"/>,
/// поэтому той же пробиркой можно засеять чашку Петри. Обработанная еда становится очагом
/// с пищевым путём: заражается только тот, кто её съест.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseContaminatorComponent : Component
{
    /// <summary>
    /// Болезнь, штамм которой заводится в пробирке.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<DiseasePrototype> Disease;

    /// <summary>
    /// Сколько порций еды ещё можно обработать.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Uses = 2;

    /// <summary>
    /// Сколько длится подмешивание.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Что можно обработать.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;
}

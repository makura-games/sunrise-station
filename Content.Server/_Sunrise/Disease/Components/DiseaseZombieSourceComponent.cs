namespace Content.Server._Sunrise.Disease.Components;

/// <summary>
/// Помечает укушенного зомби до того, как он станет носителем: хранит, кто его укусил.
/// </summary>
/// <remarks>
/// Нужен, чтобы штамм зомби шёл по цепочке заражений: укушенный болеет тем же штаммом,
/// что и укусивший, и вакцина от одной вспышки не закрывает другую.
/// Компонент одноразовый, его снимают сразу после того, как штамм унаследован.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseZombieSourceComponent : Component
{
    /// <summary>Зомби, от которого пошло заражение.</summary>
    [ViewVariables]
    public EntityUid Source;
}

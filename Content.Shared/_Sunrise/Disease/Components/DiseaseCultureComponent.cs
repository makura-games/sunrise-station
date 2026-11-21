using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Чашка Петри. Пустую засевают мазком или пробиркой со штаммом, потом культура растёт,
/// а выросшую можно отнести в диагностер как обычный образец.
/// </summary>
/// <remarks>
/// Засев предсказывается на клиенте, рост считает сервер. Клиент видит только то, что видно глазами:
/// засеяна ли чашка, выросла ли культура и есть ли в ней колонии. Какой там штамм, знает сервер.
/// Культура даёт лабораторный прогресс по штамму, но не показывает, как болезнь выглядит у людей.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class DiseaseCultureComponent : Component
{
    /// <summary>
    /// Засеянный штамм. Null, пока чашка пуста.
    /// </summary>
    [ViewVariables]
    public EntityUid? Disease;

    /// <summary>
    /// Сколько растёт культура после засева.
    /// </summary>
    [DataField]
    public TimeSpan GrowTime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Когда культура будет готова.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan ReadyAt;

    /// <summary>
    /// Засеяна ли чашка. Засевают её один раз, даже если на мазке ничего не было.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Inoculated;

    /// <summary>
    /// Выросла ли культура.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Ready;

    /// <summary>
    /// Выросли ли в чашке колонии. Чашка, засеянная чистым мазком, остаётся пустой.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HasColonies;

    /// <summary>
    /// Сколько длится засев.
    /// </summary>
    [DataField]
    public TimeSpan InoculateDelay = TimeSpan.FromSeconds(2);
}

/// <summary>
/// Ключи внешнего вида чашки Петри.
/// </summary>
[Serializable, NetSerializable]
public enum DiseaseCultureVisuals : byte
{
    /// <summary>
    /// Что сейчас в чашке. Значение — <see cref="DiseaseCultureState"/>.
    /// </summary>
    State,
}

/// <summary>
/// Что показывает чашка Петри.
/// </summary>
[Serializable, NetSerializable]
public enum DiseaseCultureState : byte
{
    /// <summary>
    /// Чашка пуста.
    /// </summary>
    Empty,

    /// <summary>
    /// Чашка засеяна, культура растёт.
    /// </summary>
    Growing,

    /// <summary>
    /// Культура выросла.
    /// </summary>
    Grown,
}

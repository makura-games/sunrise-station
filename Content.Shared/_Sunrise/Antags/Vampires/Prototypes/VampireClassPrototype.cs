using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Antags.Vampires.Prototypes;

/// <summary>
/// Data-driven определение подкласса вампира.
///
/// Для добавления нового класса вампира нужно только:
/// - новый компонент класса
/// - система для этого компонента
/// - запись <see cref="VampireClassPrototype"/> в YAML
/// </summary>
[Prototype]
public sealed partial class VampireClassPrototype : IPrototype
{
    /// <summary>
    /// Идентификатор прототипа класса.
    /// </summary>
    [IdDataField]
    public string ID { get; private set; } = default!;
    /// <summary>
    /// Ключ локализации подсказки при выборе класса.
    /// </summary>
    [DataField(required: true)]
    public string Tooltip { get; private set; } = default!;
    /// <summary>
    /// Иконка класса в меню выбора.
    /// </summary>
    [DataField(required: true)]
    public SpriteSpecifier Icon { get; private set; } = default!;
    /// <summary>
    /// Название компонента класса (например, "Hemomancer").
    /// </summary>
    [DataField(required: true)]
    public string ClassComponent { get; private set; } = default!;
    /// <summary>
    /// Список идентификаторов действий, выдаваемых классу.
    /// </summary>
    [DataField]
    public List<EntProtoId> Actions { get; private set; } = new();
}

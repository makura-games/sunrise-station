using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Выдача базовых способностей вампира через грант-компонент (паттерн ActionGrantComponent).
/// Компонент-конфиг: перечисляет базовые акшены, которые выдаются при старте
/// и снимаются при завершении роли вампира.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VampireActionGrantComponent : Component
{
    /// <summary>
    /// Базовые акшены вампира, выдаваемые при старте роли.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<EntProtoId> Actions = new();
}

using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Запрещает брать предмет в несколько рук до нужного шага туториала.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialWieldSoftLockComponent : Component
{
    /// <summary>
    /// Сообщение при попытке взять предмет в несколько рук.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-wield-disabled";
}

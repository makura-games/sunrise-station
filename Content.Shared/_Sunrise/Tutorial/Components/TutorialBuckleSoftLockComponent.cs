using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Запрещает пристёгивание игрока к стульям и другим сиденьям во время обучения.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialBuckleSoftLockComponent : Component
{
    /// <summary>
    /// Сообщение при попытке сесть.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-buckle-disabled";
}

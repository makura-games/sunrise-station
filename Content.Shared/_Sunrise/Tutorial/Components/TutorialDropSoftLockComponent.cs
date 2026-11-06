using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
///     Временно блокирует сброс предметов в активном шаге туториала.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialDropSoftLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-action-blocked";
}

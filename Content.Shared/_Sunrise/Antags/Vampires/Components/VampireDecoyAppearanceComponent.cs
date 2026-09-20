using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Заставляет созданную приманку копировать визуальные данные своего вампира.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class VampireDecoyAppearanceComponent : Component
{
    /// <summary>
    /// Сущность, которую нужно визуально дублировать.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? Source;
}

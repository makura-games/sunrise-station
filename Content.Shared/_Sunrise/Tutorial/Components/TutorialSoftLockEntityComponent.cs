using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Сущность которая должна быть заблокирована.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialSoftLockEntityComponent : Component
{
    /// <summary>
    /// Игроки, для которых сущность участвует в текущем softlock.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public HashSet<EntityUid> Players = [];
}

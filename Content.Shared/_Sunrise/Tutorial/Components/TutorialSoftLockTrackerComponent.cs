using System.Collections.Generic;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Хранит сущности, помеченные storage softlock для текущего шага туториала.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialSoftLockTrackerComponent : Component
{
    /// <summary>
    /// Сущности с временным storage softlock текущего игрока.
    /// </summary>
    public HashSet<EntityUid> LinkedEntities = [];
}

using Content.Server.Objectives.Components;
using Content.Shared.Mind;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Provides api for listings with <c>ObjectiveUnlockRequirement</c> to use.
/// </summary>
public sealed partial class StoreUnlockerSystem : EntitySystem
{
    [Dependency] private EntityQuery<StoreUnlockerComponent> _storeUnlockerQuery = default!;

    public override void Initialize()
    {
    }

    /// <summary>
    /// Returns true if a listing id is unlocked by any objectives on a mind.
    /// </summary>
    public bool IsUnlocked(MindComponent mind, string id)
    {
        foreach (var obj in mind.Objectives)
        {
            if (!_storeUnlockerQuery.TryComp(obj, out var comp))
                continue;

            if (comp.Listings.Contains(id))
                return true;
        }

        return false;
    }
}

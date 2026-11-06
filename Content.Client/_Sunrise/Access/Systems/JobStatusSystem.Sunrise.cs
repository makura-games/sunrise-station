using Content.Shared.StatusIcon.Components;
using Content.Shared.Standing;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Access.Systems;

public sealed partial class JobStatusSystem
{
    /* Совместимость служебных иконок с визуальным состоянием лежащего персонажа. */
    [Dependency] private StandingStateSystem _standing = default!;

    private bool CanShowSunriseJobStatusIcons(Entity<JobStatusComponent> ent)
    {
        return !_standing.IsDown(ent.Owner);
    }
}

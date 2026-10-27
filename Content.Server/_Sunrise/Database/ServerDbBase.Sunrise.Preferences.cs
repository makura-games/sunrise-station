using System.Linq;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    private static void StoreSunriseProfileData(Profile profile, HumanoidCharacterProfile humanoid)
    {
        profile.Voice = humanoid.Voice;
        profile.BodyType = humanoid.BodyType;
        profile.Width = humanoid.Width;
        profile.Height = humanoid.Height;

        profile.JobAlternativeTitles.Clear();
        profile.JobAlternativeTitles.AddRange(
            humanoid.JobAlternativeTitles.Select(job => new JobAlternativeTitle
            {
                JobName = job.Key,
                Title = job.Value.Id,
            }));
    }
}

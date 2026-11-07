using Content.Client.Lobby.UI.Roles;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void SetupSunriseAlternativeJobTitles(JobPrototype job, RequirementsSelector selector)
    {
        if (job.AlternativeTitles.Count == 0)
            return;

        var titleButton = new OptionButton
        {
            Margin = new Thickness(5f, 0f),
        };

        titleButton.AddItem(job.LocalizedName, 0);

        for (var i = 0; i < job.AlternativeTitles.Count; i++)
        {
            titleButton.AddItem(Loc.GetString(job.AlternativeTitles[i]), i + 1);
        }

        if (Profile != null && Profile.JobAlternativeTitles.TryGetValue(job.ID, out var savedTitle))
        {
            var index = job.AlternativeTitles.IndexOf(savedTitle);
            if (index >= 0)
                titleButton.SelectId(index + 1);
        }

        titleButton.OnItemSelected += args =>
        {
            titleButton.SelectId(args.Id);
            Profile = Profile?.WithJobAlternativeTitle(
                job.ID,
                args.Id == 0 ? (LocId?)null : job.AlternativeTitles[args.Id - 1]);
            SetDirty();
        };

        selector.ReplaceTitleWith(titleButton);
    }
}

using System.Linq;
using Content.Client._Sunrise.UserInterface.Controls;
using Robust.Client.UserInterface.Controls;
using GhostWarpPlayer = Content.Shared.Ghost.Systems.SharedGhostSystem.GhostWarpPlayer;
using GhostWarpPlace = Content.Shared.Ghost.Systems.SharedGhostSystem.GhostWarpPlace;
using GhostWarpGlobalAntagonist = Content.Shared.Ghost.Systems.SharedGhostSystem.GhostWarpGlobalAntagonist;
using Robust.Shared.Utility;


namespace Content.Client._Sunrise.UserInterface.Systems.Ghost.Controls;

public sealed partial class SunriseGhostTargetWindow
{
    private static readonly Color AntagonistButtonColor = Color.FromHex("#7F4141");
    private static readonly Color PlaceButtonColor = Color.FromHex("#969696");

    private const int DefaultButtonWidth = 200;
    private const int DefaultButtonHeight = 35;
    private const float DefaultTooltipDelay = 0.1f;

    private const int MaxLenght = 15;
    private const int MaxLenghtWithoutIcons = 18;

    // TODO: Дедупликация одинакового кода генерации
    // UDPATE: Сначала я хотел это сделать, но потом понял, что AddPlayerButtons разительно отличается от остальных
    // Так как в кнопках игроков идет итерация двух циклов, а у остальных один. И реализация generic метода не улучшит читабельность
    // И просто усложнит понимание кода, структуры генерируемого куса и не принесет никакой пользы, кроме галочки за соблюдение DRY паттерна
    // TODO-2: Придумать как реализовать generic метод, не усложняя понимание кода, без вреда читабельности

    private void AddPlayerButtons(List<GhostWarpPlayer> warps, string text)
    {
        if (warps.Count == 0)
            return;

        var bigGrid = new GridContainer();

        var bigLabel = new Label
        {
            Text = Loc.GetString(text),
            StyleClasses = { "LabelBig" },
        };

        bigGrid.AddChild(bigLabel);

        var sortedWarps = GroupPlayersByDepartment(warps)
            .OrderByDescending(kvp => kvp.Key.Weight)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        foreach (var (department, players) in sortedWarps)
        {
            var departmentGrid = new GridContainer
            {
                Columns = 5,
            };

            var departmentLabel = new Label
            {
                Text = Loc.GetString(department.Name) + ": " + players.Count,
                StyleClasses = { "LabelSecondaryColor" },
            };

            foreach (var player in players)
            {
                var playerButton = new RichTextButton
                {
                    ModulateSelfOverride = department.Color,
                    Text = GeneratePlayerLabel(player),
                    TextAlign = Label.AlignMode.Right,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center,
                    SizeFlagsStretchRatio = 1,
                    ToolTip = GeneratePlayerTooltip(player),
                    TooltipDelay = DefaultTooltipDelay,
                    SetWidth = DefaultButtonWidth,
                    SetHeight = DefaultButtonHeight,
                };

                playerButton.OnPressed += _ => WarpClicked?.Invoke(player.Entity);

                departmentGrid.AddChild(playerButton);
            }

            bigGrid.AddChild(departmentLabel);
            bigGrid.AddChild(departmentGrid);
        }

        GhostTeleportContainer.AddChild(bigGrid);
    }

    private void AddPlaceButtons(List<GhostWarpPlace> places, string text)
    {
        if (places.Count == 0)
            return;

        var bigGrid = new GridContainer();

        var bigLabel = new Label
        {
            Text = Loc.GetString(text),
            StyleClasses = { "LabelBig" },
        };
        bigGrid.AddChild(bigLabel);

        var placesGrid = new GridContainer
        {
            Columns = 5,
        };

        var countLabel = new Label
        {
            Text = Loc.GetString("ghost-teleport-menu-count-label") + ": " + places.Count,
            StyleClasses = { "LabelSecondaryColor" },
        };

        foreach (var place in places)
        {
            var placeButton = new RichTextButton
            {
                ModulateSelfOverride = PlaceButtonColor,
                Text = FormattedMessage.EscapeText(UiTextHelper.TruncateWithEllipsis(place.Name, MaxLenghtWithoutIcons)),
                TextAlign = Label.AlignMode.Right,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                SizeFlagsStretchRatio = 1,
                ToolTip = GenerateGenericTooltip(place.Name, place.Description),
                TooltipDelay = DefaultTooltipDelay,
                SetWidth = DefaultButtonWidth,
                SetHeight = DefaultButtonHeight,
            };

            placeButton.OnPressed += _ => WarpClicked?.Invoke(place.Entity);

            placesGrid.AddChild(placeButton);
        }

        bigGrid.AddChild(countLabel);
        bigGrid.AddChild(placesGrid);

        GhostTeleportContainer.AddChild(bigGrid);
    }

    private void AddAntagButtons(List<GhostWarpGlobalAntagonist> antags, string text)
    {
        if (antags.Count == 0)
            return;

        var bigGrid = new GridContainer();

        var bigLabel = new Label
        {
            Text = Loc.GetString(text),
            StyleClasses = { "LabelBig" },
        };
        bigGrid.AddChild(bigLabel);

        var sortedAntags = SortAntagsByPriority(antags);

        foreach (var antagSet in sortedAntags)
        {
            var departmentGrid = new GridContainer
            {
                Columns = 5,
            };

            var labelText = string.Empty;

            foreach (var antag in antagSet)
            {
                var playerButton = new RichTextButton
                {
                    ModulateSelfOverride = AntagonistButtonColor,
                    Text = FormattedMessage.EscapeText(UiTextHelper.TruncateWithEllipsis(antag.Name, MaxLenghtWithoutIcons)),
                    TextAlign = Label.AlignMode.Right,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center,
                    SizeFlagsStretchRatio = 1,
                    ToolTip = GenerateGenericTooltip(antag.Name, Loc.GetString(antag.AntagonistDescription)),
                    TooltipDelay = DefaultTooltipDelay,
                    SetWidth = DefaultButtonWidth,
                    SetHeight = DefaultButtonHeight,
                };

                playerButton.OnPressed += _ => WarpClicked?.Invoke(antag.Entity);

                departmentGrid.AddChild(playerButton);

                labelText = antag.AntagonistName;
            }

            var departmentLabel = new Label
            {
                Text = Loc.GetString(labelText) + ": " + antagSet.Count,
                StyleClasses = { "LabelSecondaryColor" },
            };

            bigGrid.AddChild(departmentLabel);
            bigGrid.AddChild(departmentGrid);
        }

        GhostTeleportContainer.AddChild(bigGrid);
    }
}

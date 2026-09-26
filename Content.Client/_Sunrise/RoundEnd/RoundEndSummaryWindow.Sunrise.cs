using Content.Client._Sunrise.StatsBoard;
using Content.Client.Message;
using Content.Shared._Sunrise.StatsBoard;
using Content.Shared._Sunrise.Storyteller;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.RoundEnd;

public sealed partial class RoundEndSummaryWindow
{
    /*
     * Sunrise-specific round statistics and storyteller history tabs.
     */
    [Dependency] private ISharedPlayerManager _player = default!;

    /// <summary>
    /// Creates the upstream round-end window and adds Sunrise statistics and storyteller information.
    /// </summary>
    public RoundEndSummaryWindow(
        string gamemode,
        string roundEnd,
        TimeSpan roundDuration,
        int roundId,
        RoundEndMessageEvent.RoundEndPlayerInfo[] players,
        string roundEndStats,
        SharedStatisticEntry[] statisticEntries,
        string? storytellerName,
        StorytellerHistoryEntry[] storytellerHistory)
        : this(gamemode, roundEnd, roundDuration, roundId, players)
    {
        MinSize = SetSize = new(750, 650);

        if (ContentsContainer.GetChild(0) is not TabContainer tabs ||
            tabs.GetChild(0) is not BoxContainer summaryTab)
        {
            return;
        }

        AddStorytellerName(summaryTab, storytellerName);

        var historyTab = MakeStorytellerHistoryTab(storytellerHistory);
        tabs.AddChild(historyTab);
        historyTab.SetPositionInParent(0);

        var statsTab = MakeRoundEndStatsTab(roundEndStats);
        tabs.AddChild(statsTab);
        statsTab.SetPositionInParent(1);

        var personalStatsTab = MakeRoundEndMyStatsTab(statisticEntries);
        tabs.AddChild(personalStatsTab);
        personalStatsTab.SetPositionInParent(2);
    }

    private static void AddStorytellerName(BoxContainer summaryTab, string? storytellerName)
    {
        if (string.IsNullOrEmpty(storytellerName) ||
            summaryTab.GetChild(0) is not ScrollContainer scroll ||
            scroll.GetChild(0) is not BoxContainer summary)
        {
            return;
        }

        var label = new RichTextLabel();
        label.SetMarkup(Loc.GetString(
            "round-end-summary-window-storyteller-name-label",
            ("storyteller", storytellerName)));
        summary.AddChild(label);
        label.SetPositionInParent(1);
    }

    private static BoxContainer MakeRoundEndStatsTab(string stats)
    {
        var tab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-stats-tab-title")
        };

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(10)
        };
        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical
        };

        if (!string.IsNullOrEmpty(stats))
        {
            var statsLabel = new RichTextLabel();
            statsLabel.SetMarkup(stats);
            container.AddChild(statsLabel);
        }

        scroll.AddChild(container);
        tab.AddChild(scroll);
        return tab;
    }

    private BoxContainer MakeRoundEndMyStatsTab(SharedStatisticEntry[] statisticEntries)
    {
        var tab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-my-stats-tab-title")
        };

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(10)
        };

        var statsEntries = new StatsEntries();
        var localSession = _player.LocalSession;
        if (localSession != null)
        {
            foreach (var entry in statisticEntries)
            {
                if (entry.FirstActor != localSession.UserId)
                    continue;

                statsEntries.AddEntry(new StatsEntry(
                    entry.Name,
                    entry.TotalTakeDamage,
                    entry.TotalTakeHeal,
                    entry.TotalInflictedDamage,
                    entry.TotalInflictedHeal,
                    entry.SlippedCount,
                    entry.CreamedCount,
                    entry.DoorEmagedCount,
                    entry.ElectrocutedCount,
                    entry.CuffedCount,
                    entry.AbsorbedPuddleCount,
                    entry.SpentTk ?? 0,
                    entry.DeadCount,
                    entry.HumanoidKillCount,
                    entry.KilledMouseCount,
                    entry.CuffedTime,
                    entry.SpaceTime,
                    entry.SleepTime,
                    entry.IsInteractedCaptainCard
                        ? Loc.GetString("accept-cloning-window-accept-button")
                        : Loc.GetString("accept-cloning-window-deny-button")));
            }
        }

        scroll.AddChild(statsEntries);
        tab.AddChild(scroll);
        return tab;
    }

    private static Color GetEventTypeColor(StorytellerHistoryType type)
    {
        return type switch
        {
            StorytellerHistoryType.HelpfulEvent => Color.FromHex("#2ecc71").WithAlpha(0.2f),
            StorytellerHistoryType.NeutralEvent => Color.FromHex("#7f8c8d").WithAlpha(0.2f),
            StorytellerHistoryType.MinorCalmEvent => Color.FromHex("#3498db").WithAlpha(0.2f),
            StorytellerHistoryType.MajorCalmEvent => Color.FromHex("#9b59b6").WithAlpha(0.2f),
            StorytellerHistoryType.MinorAntagEvent => Color.FromHex("#e67e22").WithAlpha(0.2f),
            StorytellerHistoryType.MajorAntagEvent => Color.FromHex("#e74c3c").WithAlpha(0.2f),
            StorytellerHistoryType.Death => Color.FromHex("#c0392b").WithAlpha(0.2f),
            StorytellerHistoryType.AnomalyEngine => Color.FromHex("#e67e22").WithAlpha(0.2f),
            StorytellerHistoryType.StationEvent => Color.FromHex("#34495e").WithAlpha(0.2f),
            StorytellerHistoryType.Explosion => Color.FromHex("#d35400").WithAlpha(0.2f),
            StorytellerHistoryType.Research => Color.FromHex("#9b59b6").WithAlpha(0.2f),
            StorytellerHistoryType.Arrival => Color.FromHex("#2ecc71").WithAlpha(0.2f),
            StorytellerHistoryType.Departure => Color.FromHex("#3498db").WithAlpha(0.2f),
            _ => Color.FromHex("#95a5a6").WithAlpha(0.2f)
        };
    }

    private static BoxContainer MakeStorytellerHistoryTab(StorytellerHistoryEntry[] history)
    {
        var tab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-storyteller-history-tab-title")
        };

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(10),
            HScrollEnabled = false
        };
        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 10
        };
        var eventPanels = new List<(StorytellerHistoryType Type, Control Panel)>();

        if (history.Length == 0)
        {
            var emptyLabel = new RichTextLabel();
            emptyLabel.SetMarkup(Loc.GetString("round-end-summary-window-storyteller-history-empty"));
            container.AddChild(emptyLabel);
        }
        else
        {
            var titleLabel = new RichTextLabel();
            titleLabel.SetMarkup($"[bold][font size=14]{Loc.GetString("round-end-summary-window-storyteller-history-tab-title")}[/font][/bold]\n");
            container.AddChild(titleLabel);

            foreach (var entry in history)
            {
                var panelColor = GetEventTypeColor(entry.EventType);
                var panel = new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = panelColor,
                        BorderColor = panelColor.WithAlpha(1f),
                        BorderThickness = new Thickness(1),
                        ContentMarginBottomOverride = 6,
                        ContentMarginLeftOverride = 6,
                        ContentMarginRightOverride = 6,
                        ContentMarginTopOverride = 6
                    }
                };

                var row = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    SeparationOverride = 12
                };
                var timeLabel = new RichTextLabel { SetWidth = 80 };
                var formattedTime = entry.RoundTime.ToString(@"hh\:mm\:ss");
                timeLabel.SetMarkup($"[color=#A9A9A9]({formattedTime})[/color]");

                var textLabel = new RichTextLabel { HorizontalExpand = true };
                textLabel.SetMarkupPermissive(entry.Description);

                row.AddChild(timeLabel);
                row.AddChild(textLabel);
                panel.AddChild(row);
                container.AddChild(panel);
                eventPanels.Add((entry.EventType, panel));
            }
        }

        scroll.AddChild(container);
        tab.AddChild(scroll);

        if (history.Length == 0)
            return tab;

        var filters = new GridContainer
        {
            Columns = 4,
            Margin = new Thickness(10, 0, 10, 10)
        };
        var categories = new Dictionary<string, List<StorytellerHistoryType>>
        {
            [Loc.GetString("storyteller-history-filter-events")] =
            new List<StorytellerHistoryType>
            {
                StorytellerHistoryType.HelpfulEvent,
                StorytellerHistoryType.NeutralEvent,
                StorytellerHistoryType.MinorCalmEvent,
                StorytellerHistoryType.MajorCalmEvent
            },
            [Loc.GetString("storyteller-history-filter-antagonists")] =
            new List<StorytellerHistoryType>
            {
                StorytellerHistoryType.MinorAntagEvent,
                StorytellerHistoryType.MajorAntagEvent
            },
            [Loc.GetString("storyteller-history-filter-station")] = new List<StorytellerHistoryType>
            {
                StorytellerHistoryType.StationEvent
            },
            [Loc.GetString("storyteller-history-filter-deaths")] = new List<StorytellerHistoryType>
            {
                StorytellerHistoryType.Death
            },
            [Loc.GetString("storyteller-history-filter-anomalies")] = new List<StorytellerHistoryType>
            {
                StorytellerHistoryType.AnomalyEngine
            },
            [Loc.GetString("storyteller-history-filter-explosions")] = new List<StorytellerHistoryType>
            {
                StorytellerHistoryType.Explosion
            },
            [Loc.GetString("storyteller-history-filter-research")] = new List<StorytellerHistoryType>
            {
                StorytellerHistoryType.Research
            },
            [Loc.GetString("storyteller-history-filter-arrivals")] = new List<StorytellerHistoryType>
            {
                StorytellerHistoryType.Arrival
            },
            [Loc.GetString("storyteller-history-filter-cryo")] = new List<StorytellerHistoryType>
            {
                StorytellerHistoryType.Departure
            }
        };

        foreach (var (label, eventTypes) in categories)
        {
            var hiddenByDefault = eventTypes.Contains(StorytellerHistoryType.Arrival) ||
                                  eventTypes.Contains(StorytellerHistoryType.Departure);
            var checkBox = new CheckBox
            {
                Text = label,
                Pressed = !hiddenByDefault
            };

            checkBox.OnToggled += args =>
            {
                foreach (var (eventType, panel) in eventPanels)
                {
                    if (eventTypes.Contains(eventType))
                        panel.Visible = args.Pressed;
                }
            };

            foreach (var (eventType, panel) in eventPanels)
            {
                if (eventTypes.Contains(eventType))
                    panel.Visible = !hiddenByDefault;
            }

            filters.AddChild(checkBox);
        }

        tab.AddChild(filters);
        return tab;
    }
}

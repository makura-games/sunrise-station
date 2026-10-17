using System.Linq;
using Content.Shared._Sunrise.InteractionsPanel.Data.Components;
using Content.Shared._Sunrise.InteractionsPanel.Data.Prototypes;
using Content.Shared._Sunrise.InteractionsPanel.Data.UI;
using Content.Shared.Database;

namespace Content.Server._Sunrise.InteractionsPanel;

public partial class InteractionsPanel
{
    private void InitializeUI()
    {
        SubscribeLocalEvent<InteractionsComponent, BoundUIOpenedEvent>(OnBUIOpened);
        SubscribeLocalEvent<InteractionsComponent, BoundUserInterfaceCheckRangeEvent>(OnCheckRange);
        SubscribeLocalEvent<InteractionsComponent, BoundUIClosedEvent>(OnAfterUiClose);
    }

    private void OnBUIOpened(Entity<InteractionsComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!args.UiKey.Equals(InteractionWindowUiKey.Key))
            return;

        if (!ent.Comp.CurrentTarget.HasValue)
            return;

        var state = PrepareUIState(ent, ent.Comp.CurrentTarget.Value);
        _ui.SetUiState(ent.Owner, InteractionWindowUiKey.Key, state);
    }

    private void OnCheckRange(Entity<InteractionsComponent> ent, ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (!args.UiKey.Equals(InteractionWindowUiKey.Key))
            return;

        if (args.Result == BoundUserInterfaceRangeResult.Fail)
            return;

        if (!ent.Comp.CurrentTarget.HasValue)
        {
            args.Result = BoundUserInterfaceRangeResult.Fail;
            return;
        }

        if (!_interaction.InRangeUnobstructed(args.Target, ent.Comp.CurrentTarget.Value))
            args.Result = BoundUserInterfaceRangeResult.Fail;
    }

    private void OnAfterUiClose(Entity<InteractionsComponent> ent, ref BoundUIClosedEvent args)
    {
        ent.Comp.CurrentTarget = null;
        Dirty(ent);
    }

    private void OpenUI(Entity<UserInterfaceComponent?> user, EntityUid target)
    {
        var state = PrepareUIState(user, target);

        if (_ui.TryOpenUi(user, InteractionWindowUiKey.Key, user))
        {
            _ui.SetUiState(user, InteractionWindowUiKey.Key, state);
        }

        if (TryComp<InteractionsComponent>(user, out var interactions))
        {
            interactions.CurrentTarget = target;
            Dirty(user, interactions);
        }

        _log.Add(LogType.Interactions, LogImpact.Low,
            $"[InteractionsPanel] {ToPretty(user)} открыл панель взаимодействий с {ToPretty(target)}");
    }

    private InteractionWindowBoundUserInterfaceState PrepareUIState(EntityUid user, EntityUid target)
    {

        var availableInteractions = FetchInteractions(user, target);
        var interactionIds = availableInteractions.Select(i => i.ID).ToList();

        var userNet = GetNetEntity(user);
        var targetNet = GetNetEntity(target);

        return new InteractionWindowBoundUserInterfaceState(
            userNet,
            targetNet,
            interactionIds
        );
    }

    private List<InteractionPrototype> FetchInteractions(EntityUid user, EntityUid target)
    {
        var allInteractions = ProtoMan.EnumeratePrototypes<InteractionPrototype>();
        var availableInteractions = new List<InteractionPrototype>();

        foreach (var interaction in allInteractions)
        {

            if (interaction.AppearConditions.Count == 0)
            {
                availableInteractions.Add(interaction);
                continue;
            }

            if (CheckAllAppearConditions(interaction, user, target))
            {
                availableInteractions.Add(interaction);
            }
        }

        return availableInteractions;
    }
}

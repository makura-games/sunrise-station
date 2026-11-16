using System.Collections.Generic;
using Content.Server.EUI;
using Content.Shared._Sunrise.Tutorial.Eui;
using Content.Shared.Eui;

namespace Content.Server._Sunrise.Tutorial;

public sealed partial class TutorialCompletionEui : BaseEui
{
    [Dependency] private IEntitySystemManager _entitySystem = default!;
    private readonly EntityUid _player;
    private readonly TutorialSystem _system;

    public TutorialCompletionEui(EntityUid player)
    {
        IoCManager.InjectDependencies(this);
        _player = player;
        _system = _entitySystem.GetEntitySystem<TutorialSystem>();
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override void Closed()
    {
        _system.OnCompletionEuiClosed(Player);
    }

    public override EuiStateBase GetNewState()
    {
        var actions = new List<TutorialCompletionEuiAction>
        {
        };

        if (_system.TryGetNextTutorial(_player, out _))
        {
            actions.Add(new TutorialCompletionEuiAction(
                TutorialCompletionActions.Next,
                "tutorial-complete-next",
                null,
                false));
        }

        actions.AddRange([
            new(TutorialCompletionActions.Leave, "tutorial-complete-leave", null, false),
            new(TutorialCompletionActions.Stay, "tutorial-complete-stay", null, false),
        ]);

        return new TutorialCompletionEuiState(
            "tutorial-complete-title",
            "tutorial-complete-desc",
            actions);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not TutorialCompletionEuiActionMessage action)
            return;

        if (_system.HandleCompletionAction(_player, action.ActionId))
            Close();
    }
}

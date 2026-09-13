using System.Numerics;
using Content.Shared._Sunrise.InteractionsPanel.Data.UI;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Client._Sunrise.InteractionsPanel;

public sealed partial class InteractionsWindowBoundUserInterface : BoundUserInterface
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private InteractionsUIWindow? _slave;

    public InteractionsWindowBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _slave = this.CreateWindow<InteractionsUIWindow>();
        _slave.SetOwner(this);
    }

    protected override void Open()
    {
        base.Open();

        var savedPosX = _cfg.GetCVar(InteractionsCVars.WindowPosX);
        var savedPosY = _cfg.GetCVar(InteractionsCVars.WindowPosY);
        var savedPosVector = new Vector2(savedPosX, savedPosY);
        if (!savedPosVector.Equals(Vector2.Zero))
        {
            _slave?.Open(savedPosVector);
        }
        else
        {
            _slave?.OpenCentered();
        }

    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is RequestSavePosAndCloseMessage save)
        {
            _slave?.Close();
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is InteractionWindowBoundUserInterfaceState interactionsState && _slave != null)
        {
            _slave.UpdateState(
                interactionsState.UserEntity,
                interactionsState.TargetEntity,
                interactionsState.AvailableInteractionIds
            );
        }
    }

    public void SendBoundUserInterfaceMessage(BoundUserInterfaceMessage message)
    {
        SendMessage(message);
    }
}

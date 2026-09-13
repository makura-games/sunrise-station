using Content.Client.Gameplay;
using Content.Shared._Sunrise.Messenger;
using Content.Shared.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input.Binding;
using Robust.Shared.GameObjects;

namespace Content.Client._Sunrise.UserInterface.Systems.Messenger;

public sealed partial class MessengerUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IEntityNetworkManager _netManager = default!;

    public void OnStateEntered(GameplayState state)
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenMessenger, InputCmdHandler.FromDelegate(_ => OnOpenMessenger()))
            .Register<MessengerUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<MessengerUIController>();
    }

    private void OnOpenMessenger()
    {
        _netManager.SendSystemNetworkMessage(new OpenMessengerRequestEvent());
    }
}

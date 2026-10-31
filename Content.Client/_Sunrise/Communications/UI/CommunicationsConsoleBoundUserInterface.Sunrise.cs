using Content.Shared.Communications;

namespace Content.Client.Communications.UI;

public sealed partial class CommunicationsConsoleBoundUserInterface
{
    private void ToggleRelayPressed()
    {
        SendMessage(new CommunicationsConsoleToggleRelayMessage());
    }
}

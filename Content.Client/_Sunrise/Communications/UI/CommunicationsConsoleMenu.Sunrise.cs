using Content.Shared.Communications;

namespace Content.Client.Communications.UI;

public sealed partial class CommunicationsConsoleMenu
{
    public event Action? OnToggleRelay;

    public void UpdateSunriseState(CommunicationsConsoleInterfaceState state)
    {
        RelayControls.UpdateState(
            state.CanRelay,
            state.IsRelaying,
            state.RelayCooldownRemaining,
            state.RelayTimeRemaining);
    }
}

using Content.Shared.Access.Systems;
using Content.Shared.Communications;
using Robust.Client.Player;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-интерфейсу.
namespace Content.Client.Communications.UI;

public sealed partial class CommunicationsConsoleBoundUserInterface
{
    /*
     * Client-side validation and messages for alert-level controls.
     */

    [Dependency] private readonly IPlayerManager _player = default!;

    private AccessReaderSystem AccessReader => EntMan.System<AccessReaderSystem>();

    private void AdditionalAlertLevelSelected(string level, bool enabled)
    {
        if (!HasAccess())
            return;

        SendMessage(new CommunicationsConsoleSetAdditionalAlertLevelMessage(level, enabled));
    }

    private void AlertStationSelected(NetEntity station)
    {
        if (!HasAccess() || _menu == null)
            return;

        // Ждём подтверждённое состояние выбранной станции, чтобы команда не ушла на предыдущую.
        _menu.DisableAlertLevelControls();
        SendMessage(new CommunicationsConsoleSelectAlertStationMessage(station));
    }

    private bool HasAccess()
    {
        return _player.LocalEntity is { } player && AccessReader.IsAllowed(player, Owner);
    }
}

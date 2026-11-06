using Content.Server._Sunrise.Sandbox.DeviceLink;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Sunrise.Chat.Commands;

/// <summary>
///     Toggles the display of connected device links.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class ShowDeviceLinkCommand : LocalizedEntityCommands
{
    [Dependency] private DeviceLinkingVisualizationSystem _deviceLinking = default!;

    public override string Command => "showdevicelink";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player == null)
        {
            shell.WriteError(LocalizationManager.GetString($"cmd-{Command}-denied"));
            return;
        }

        _deviceLinking.ToggleDebugView(shell.Player);
        shell.WriteLine(LocalizationManager.GetString($"cmd-{Command}-status"));
    }
}

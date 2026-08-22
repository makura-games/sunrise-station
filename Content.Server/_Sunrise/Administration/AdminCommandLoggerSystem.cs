using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Shared.Database;
using Robust.Shared.Console;
using Robust.Shared.Toolshed;

namespace Content.Server._Sunrise.Administration;

/// <summary>
/// Система, логгирующая введенные админ-команды в админ-логи.
/// Заставляет админов чувствовать себя уязвимыми.
/// </summary>
// TODO: Сделать дискорд вебхук с этим
public sealed partial class AdminCommandLoggerSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private ToolshedManager _toolshed = default!;

    /// <summary>
    /// Черный список команд, которые не должны логгироваться.
    /// </summary>
    /// <remarks>
    /// При добавлении учитывать, что команды не требующие админ-прав игнорируются по умолчанию!
    /// </remarks>
    private static readonly HashSet<string> LogBlacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "asay",
        "deadmin",
        "readmin",
    };

    #region Life cycle

    public override void Initialize()
    {
        _console.AnyCommandExecuted += OnCommandExecuted;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _console.AnyCommandExecuted -= OnCommandExecuted;
    }

    #endregion

    private void OnCommandExecuted(IConsoleShell shell, string name, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
            return;

        if (!ShouldLog(name, args))
            return;

        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.Extreme,
            $"Administrator {player:player} executed command [{argStr}]");
    }

    private bool ShouldLog(string name, string[] args)
    {
        if (LogBlacklist.Contains(name))
            return false;

        var adminOnly = false;

        if (_toolshed.DefaultEnvironment.TryGetCommand(name, out var tsCmd))
        {
            var sub = tsCmd.HasSubCommands && args.Length > 0 ? args[0] : null;
            var commandSpec = new CommandSpec(tsCmd, sub);

            adminOnly = _admin.TryGetCommandFlags(commandSpec, out var flags) && flags != null;
        }
        else if (_console.AvailableCommands.TryGetValue(name, out var consoleCommand))
            adminOnly = Attribute.IsDefined(consoleCommand.GetType(), typeof(AdminCommandAttribute));

        if (!adminOnly)
            return false;

        return true;
    }
}

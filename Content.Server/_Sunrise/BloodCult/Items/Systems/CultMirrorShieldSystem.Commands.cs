using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Sunrise.BloodCult.Items.Systems;

/// <summary>
/// Эта часть содержит вспомогательные команды
/// </summary>
public sealed partial class CultMirrorShieldSystem
{
    [Dependency] private IConsoleHost _console = default!;

    private void InitializeCommands()
    {
        _console.RegisterCommand("cultmirror_shatter", ShatterMirrorCallback);
        _console.RegisterCommand("cultmirror_clone", CloneCallback);
    }

    [AdminCommand(AdminFlags.Debug)]
    private void CloneCallback(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length > 1)
            return;

        if (shell.Player?.AttachedEntity is not { } entity)
        {
            shell.WriteError("This command can only be ran by a player with an attached entity.");
            return;
        }

        if (args.Length == 1 && !EntityUid.TryParse(args[0], out entity))
        {
            shell.WriteError("Invalid entity uid.");
            return;
        }

        // Clone behavior
        shell.WriteLine($"CreateIllusion returned: {CreateIllusion(entity, out _)}");
    }

    [AdminCommand(AdminFlags.Debug)]
    private void ShatterMirrorCallback(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length > 1)
            return;

        if (shell.Player?.AttachedEntity is not { } entity)
        {
            shell.WriteError("This command can only be ran by a player with an attached entity.");
            return;
        }

        if (args.Length == 1 && !EntityUid.TryParse(args[0], out entity))
        {
            shell.WriteError("Invalid entity uid.");
            return;
        }

        foreach (var hand in _hands.EnumerateHands(entity))
        {
            var heldEntity = _hands.GetHeldItem(entity, hand);
            if (heldEntity is null)
                continue;
            BreakShield(heldEntity.Value);
        }
    }
}

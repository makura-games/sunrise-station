// Sunrise-Edit - Force trigger storyteller events for testing
using System.Linq;
using Content.Server._Sunrise.Storyteller.Systems;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Sunrise.Storyteller.Prototypes;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Storyteller.Commands
{
    [AdminCommand(AdminFlags.Admin)]
    public sealed partial class ForceStorytellerEventCommand : IConsoleCommand
    {
        [Dependency] private IEntityManager _entManager = default!;

        public string Command => "forcestorytellerevent";
        public string Description => "Force triggers a storyteller event, bypassing all stress, threat budget, and state restrictions.";
        public string Help => "Usage: forcestorytellerevent <event/gamerule prototype id>";

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            if (args.Length == 1)
            {
                var protoManager = IoCManager.Resolve<IPrototypeManager>();
                var options = protoManager.EnumeratePrototypes<StorytellerMetadataPrototype>()
                    .Select(p => p.ID)
                    .OrderBy(id => id)
                    .ToList();
                return CompletionResult.FromHintOptions(options, "event prototype id");
            }
            return CompletionResult.Empty;
        }

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteError("Usage: forcestorytellerevent <event/gamerule prototype id>");
                return;
            }

            var eventId = args[0];
            var storytellerSystem = _entManager.System<StorytellerSystem>();

            if (storytellerSystem.ForceTriggerEvent(eventId))
            {
                shell.WriteLine($"Successfully force triggered storyteller event '{eventId}'.");
            }
            else
            {
                shell.WriteError($"Failed to force trigger storyteller event '{eventId}'. Ensure the storyteller game rule is active and the event/gamerule prototype exists.");
            }
        }
    }
}

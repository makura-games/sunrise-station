using Robust.Shared.Console;
using Robust.Shared.Random;

namespace Content.Client._Sunrise.UserInterface.Radial;

public sealed partial class RadialContainerCommandTest : LocalizedCommands
{
    [Dependency] private IRobustRandom _robustRandom = default!;

    public override string Command => "radialtest";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        string[] tips =
        {
            "Testovый туултип. Здесь можете расписать разную инфу о кнопке/действии",
            "Из окна дуло. Штирлиц закрыл окно. Дуло исчезло.",
        };
        var radial = new RadialContainer();
        for (int i = 0; i < 8; i++)
        {
            var testButton = radial.AddButton("Action " + i, "/Textures/Interface/emotions.svg.192dpi.png");
            testButton.Tooltip = tips[_robustRandom.Next(0, 2)];
            testButton.Controller.OnPressed += (_) => { Logger.Debug("Press gay"); };
        }

        radial.CloseButton.Controller.OnPressed += (_) =>
        {
            Logger.Debug("Close event for your own logic");
        };
        radial.OpenAttachedLocalPlayer();
    }
}

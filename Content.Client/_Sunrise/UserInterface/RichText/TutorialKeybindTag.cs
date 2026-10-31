using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;

namespace Content.Client._Sunrise.UserInterface.RichText;

[UsedImplicitly]
public sealed partial class TutorialKeybindTag : IMarkupTagHandler
{
    [Dependency] private IInputManager _inputManager = default!;

    public string Name => "tutkeybind";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (!node.Value.TryGetString(out var keyBindName) || string.IsNullOrWhiteSpace(keyBindName))
            return false;

        var keybindText = keyBindName;
        if (_inputManager.TryGetKeyBinding(keyBindName, out var binding))
            keybindText = binding.GetKeyString();

        var frame = new TutorialKeybindControl
        {
            VerticalAlignment = Control.VAlignment.Center,
        };
        frame.KeybindLabel.Text = keybindText;

        control = frame;
        return true;
    }
}

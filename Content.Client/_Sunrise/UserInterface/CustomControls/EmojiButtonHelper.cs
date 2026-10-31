// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/space-sunrise/space-station-14/blob/master/CLA.txt

using Content.Client._Sunrise.UserInterface.CustomControls;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;

namespace Content.Client._Sunrise.UserInterface.CustomControls;

/// <summary>
/// General helper for emoji button initialization and binding.
/// </summary>
public static class EmojiButtonHelper
{
    /// <summary>
    /// Initializes the emoji button and configures the picker window.
    /// Sets the icon and configures picker window opening on click.
    /// Returns an Action to close and clean up the created picker.
    /// </summary>
    public static Action SetupEmojiButton(
        BaseButton emojiButton,
        LineEdit lineEdit,
        IResourceCache resourceCache)
    {
        var texture = resourceCache.GetTexture("/Textures/_Sunrise/Interface/Smile.png");

        if (emojiButton is TextureButton texBtn)
        {
            texBtn.TextureNormal = texture;
        }
        else
        {
            emojiButton.DisposeAllChildren();
            var textureRect = new TextureRect
            {
                Texture = texture,
                SetWidth = 24,
                SetHeight = 24,
                HorizontalAlignment = Control.HAlignment.Center,
                VerticalAlignment = Control.VAlignment.Center,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
            };
            emojiButton.AddChild(textureRect);
        }

        EmojiPickerWindow? picker = null;

        emojiButton.OnPressed += _ =>
        {
            if (picker != null && picker.IsOpen)
            {
                picker.Close();
                return;
            }

            picker = new EmojiPickerWindow();
            picker.OnEmojiSelected += emojiCode =>
            {
                lineEdit.Text += emojiCode;
                lineEdit.CursorPosition = lineEdit.Text.Length;
                lineEdit.GrabKeyboardFocus();
            };

            picker.OnClose += () => picker = null;
            picker.OpenCentered();
        };

        return () =>
        {
            picker?.Close();
            picker = null;
        };
    }
}

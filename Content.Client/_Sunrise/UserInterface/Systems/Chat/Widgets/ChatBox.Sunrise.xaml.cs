using System.Linq;
using Content.Client._Sunrise.Messenger;
using Content.Client._Sunrise.UserInterface.CustomControls;
using Content.Client._Sunrise.UserInterface.RichText;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Client.UserInterface.RichText;

// ReSharper disable once CheckNamespace
namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    // По умолчаюнию разрешены только RichTextEntry.DefaultTags.
    // Теги ниже нужны для корректного отображения иконок в чате

    private static readonly Type[] BaseTags =
    [
        typeof(BoldItalicTag),
        typeof(BoldTag),
        typeof(BulletTag),
        typeof(ColorTag),
        typeof(HeadingTag),
        typeof(ItalicTag),
        typeof(TextureTag),
        typeof(EntityTextureTag),
        typeof(RadioIconTag),
    ];

    private static readonly Type[] TagsEmoji =
    [
        ..BaseTags,
        typeof(EmojiTag),
    ];

    private static readonly Type[] TagsAdminChannel =
    [
        ..TagsEmoji,
        typeof(CommandLinkTag),
    ];

    private const string VerticalScrollName = "_v_scroll";

    private EmojiSystem? _emoji;

    private EmojiPickerWindow? _emojiPicker;

    private bool _emojiButtonSubscribed;

    public void SetChatOpacity()
    {
        _controller.SetChatWindowOpacity(_configurationManager.GetCVar(CCVars.ChatWindowOpacity));
    }

    public void ToggleEmojiButton(bool visible)
    {
        ChatInput.ToggleEmojiButton(visible);
        if (ChatInput.EmojiButton != null && !_emojiButtonSubscribed)
        {
            _emojiButtonSubscribed = true;
            ChatInput.EmojiButton.OnPressed += _ =>
            {
                if (_emojiPicker != null && _emojiPicker.IsOpen)
                {
                    _emojiPicker.Close();
                    return;
                }

                _emojiPicker = new EmojiPickerWindow();
                _emojiPicker.OnEmojiSelected += emojiCode =>
                {
                    ChatInput.Input.Text += emojiCode;
                    ChatInput.Input.CursorPosition = ChatInput.Input.Text.Length;
                    ChatInput.Input.GrabKeyboardFocus();
                };
                _emojiPicker.OnClose += () => _emojiPicker = null;
                _emojiPicker.OpenCentered();
            };
        }
    }

    private void ClearChatContents()
    {
        Contents.Clear();

        foreach (var child in Contents.Children.ToArray())
        {
            if (child.Name != VerticalScrollName)
                Contents.RemoveChild(child);
        }
    }

    private static Type[] GetAllowedTags(ChatChannel channel, bool emojiAllowed)
    {
        if ((channel & ChatChannel.AdminRelated) != 0)
            return TagsAdminChannel;

        if (emojiAllowed)
            return TagsEmoji;

        return BaseTags;
    }
}

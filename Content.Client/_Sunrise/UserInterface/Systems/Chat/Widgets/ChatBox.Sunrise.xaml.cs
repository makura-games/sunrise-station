using System.Linq;
using Content.Client._Sunrise.Messenger;
using Content.Client._Sunrise.UserInterface.CustomControls;
using Content.Client._Sunrise.UserInterface.RichText;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Client.UserInterface.RichText;

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

    public void SetChatOpacity()
    {
        _controller.SetChatWindowOpacity(_configurationManager.GetCVar(CCVars.ChatWindowOpacity));
    }

    public void ToggleEmojiButton(bool visible)
    {
        ChatInput.ToggleEmojiButton(visible);
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

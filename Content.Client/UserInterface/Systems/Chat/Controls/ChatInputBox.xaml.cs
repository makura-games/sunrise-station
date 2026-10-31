using Content.Client._Sunrise.UserInterface.CustomControls;
using Content.Client.Resources;
using Content.Shared.Chat;
using Content.Shared.Input;
using Robust.Client.UserInterface.Controls;
using Robust.Client.ResourceManagement;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

[Virtual]
public class ChatInputBox : PanelContainer
{
    public const string StyleClassChatPanel = "ChatPanel";
    public const string StyleClassChatLineEdit = "ChatLineEdit";
    public const string StyleClassChatFilterOptionButton = "ChatFilterOptionButton";

    public readonly ChannelSelectorButton ChannelSelector;
    public readonly HistoryLineEdit Input;
    public readonly ChannelFilterButton FilterButton;
    public Button? EmojiButton; // Sunrise-Add
    private Action? _emojiPickerCleanup; // Sunrise-Edit
    protected readonly BoxContainer Container;
    protected ChatChannel ActiveChannel { get; private set; } = ChatChannel.Local;

    public ChatInputBox()
    {
        Container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4
        };
        AddChild(Container);

        ChannelSelector = new ChannelSelectorButton
        {
            Name = "ChannelSelector",
            ToggleMode = true,
            StyleClasses = { ChannelSelectorItemButton.StyleClassChatSelectorOptionButton },
            MinWidth = 75
        };
        Container.AddChild(ChannelSelector);
        Input = new HistoryLineEdit
        {
            Name = "Input",
            PlaceHolder = GetChatboxInfoPlaceholder(),
            HorizontalExpand = true,
            StyleClasses = { StyleClassChatLineEdit }
        };
        Container.AddChild(Input);
        FilterButton = new ChannelFilterButton
        {
            Name = "FilterButton",
            StyleClasses = { StyleClassChatFilterOptionButton }
        };
        Container.AddChild(FilterButton);
        AddStyleClass(StyleClassChatPanel);
        ChannelSelector.OnChannelSelect += UpdateActiveChannel;
    }

    // Sunrise-Start
    public void ToggleEmojiButton(bool visible)
    {
        if (visible && EmojiButton == null)
        {
            EmojiButton = new Button
            {
                Name = "EmojiButton",
                SetWidth = 30,
                ToolTip = Loc.GetString("messenger-emoji-button-tooltip")
            };

            var resourceCache = IoCManager.Resolve<IResourceCache>();
            _emojiPickerCleanup = EmojiButtonHelper.SetupEmojiButton(EmojiButton, Input, resourceCache);

            Container.AddChild(EmojiButton);
            EmojiButton.SetPositionInParent(1);
        }

        if (!visible)
            _emojiPickerCleanup?.Invoke();

        if (EmojiButton != null)
            EmojiButton.Visible = visible;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _emojiPickerCleanup?.Invoke();
    }
    // Sunrise-End

    private void UpdateActiveChannel(ChatSelectChannel selectedChannel)
    {
        ActiveChannel = (ChatChannel) selectedChannel;
    }

    private static string GetChatboxInfoPlaceholder()
    {
        return (BoundKeyHelper.IsBound(ContentKeyFunctions.FocusChat),
                BoundKeyHelper.IsBound(ContentKeyFunctions.CycleChatChannelForward)) switch
            {
                (true, true) => Loc.GetString("hud-chatbox-info",
                    ("talk-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat)),
                    ("cycle-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.CycleChatChannelForward))),
                (true, false) => Loc.GetString("hud-chatbox-info-talk",
                    ("talk-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat))),
                (false, true) => Loc.GetString("hud-chatbox-info-cycle",
                    ("cycle-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.CycleChatChannelForward))),
                (false, false) => Loc.GetString("hud-chatbox-info-unbound")
            };
    }

    // Sunrise added start - cleanup emoji picker
    [Obsolete]
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _emojiPickerCleanup?.Invoke();
    }
    // Sunrise added end
}

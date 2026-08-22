using Content.Client._Sunrise.Messenger;
using Content.Shared._Sunrise.Messenger;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Robust.Shared.Input;

namespace Content.Client._Sunrise.UserInterface.CustomControls;

public sealed partial class EmojiPickerWindow : DefaultWindow
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IResourceCache _resource = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILocalizationManager _loc = default!;

    private readonly BoxContainer? _emojiPickerContentContainer;
    private readonly List<string> _recentEmojis = [];
    private readonly HashSet<EmojiPrototype> _favoriteEmojis = [];

    public event Action<string>? OnEmojiSelected;

    private readonly EmojiSystem _emoji;
    private readonly SpriteSystem _sprite;

    public EmojiPickerWindow()
    {
        IoCManager.InjectDependencies(this);

        _emoji = _entity.System<EmojiSystem>();
        _sprite = _entity.System<SpriteSystem>();

        Title = _loc.GetString("messenger-emoji-picker-title");
        MinSize = new Vector2(445, 400);
        Resizable = false;

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(8),
        };

        var scrollContainer = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            ReserveScrollbarSpace = true,
        };

        _emojiPickerContentContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 5),
        };

        scrollContainer.AddChild(_emojiPickerContentContainer);
        container.AddChild(scrollContainer);
        Contents.AddChild(container);

        LoadSavedEmojis();
        UpdateEmojiPickerContent();
    }

    public void UpdateEmojiPickerContent()
    {
        if (_emojiPickerContentContainer == null)
            return;

        _emojiPickerContentContainer.RemoveAllChildren();

        BuildRecentEmojisSection(_emojiPickerContentContainer);
        BuildFavoriteEmojisSection(_emojiPickerContentContainer);
        BuildAllEmojisSection(_emojiPickerContentContainer);
    }

    private StyleBoxTexture CreateEmojiPanelStyle()
    {
        var panelTex = _resource.GetTexture("/Textures/Interface/Nano/rounded_button_bordered.svg.96dpi.png");
        var panelStyle = new StyleBoxTexture
        {
            Texture = panelTex,
        };
        panelStyle.SetPatchMargin(StyleBox.Margin.All, 5);
        panelStyle.SetContentMarginOverride(StyleBox.Margin.All, 8);
        panelStyle.Modulate = Color.FromHex("#2F2F35");
        return panelStyle;
    }

    private void BuildRecentEmojisSection(BoxContainer contentContainer)
    {
        var recentPanel = new PanelContainer
        {
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 10),
            PanelOverride = CreateEmojiPanelStyle(),
        };

        var recentSection = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        recentSection.AddChild(new Label
        {
            Text = _loc.GetString("messenger-emoji-recent-title"),
            StyleClasses = { "Bold" },
            Margin = new Thickness(0, 0, 0, 4),
        });

        if (_recentEmojis.Count > 0)
        {
            var recentContainer = new GridContainer
            {
                Columns = 5,
                HorizontalExpand = true,
            };
            var recentToShow = _recentEmojis.TakeLast(5).Reverse().ToList();
            foreach (var emojiCode in recentToShow)
            {
                if (_emoji.Emojis.TryGetValue(emojiCode, out var emoji))
                {
                    recentContainer.AddChild(CreateEmojiButton(emoji));
                }
            }
            recentSection.AddChild(recentContainer);
        }
        else
        {
            recentSection.AddChild(new Label
            {
                Text = _loc.GetString("messenger-emoji-recent-empty-hint"),
                StyleClasses = { "LabelSubText" },
            });
        }
        recentPanel.AddChild(recentSection);
        contentContainer.AddChild(recentPanel);
    }

    private void BuildFavoriteEmojisSection(BoxContainer contentContainer)
    {
        var favoritePanel = new PanelContainer
        {
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 10),
            PanelOverride = CreateEmojiPanelStyle(),
        };

        var favoriteSection = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        favoriteSection.AddChild(new Label
        {
            Text = _loc.GetString("messenger-emoji-favorite-title"),
            StyleClasses = { "Bold" },
            Margin = new Thickness(0, 0, 0, 4),
        });

        if (_favoriteEmojis.Count > 0)
        {
            var favoriteContainer = new GridContainer
            {
                Columns = 5,
                HorizontalExpand = true,
            };
            foreach (var emoji in _favoriteEmojis)
            {
                favoriteContainer.AddChild(CreateEmojiButton(emoji));
            }
            favoriteSection.AddChild(favoriteContainer);
        }
        else
        {
            favoriteSection.AddChild(new Label
            {
                Text = _loc.GetString("messenger-emoji-favorite-hint"),
                StyleClasses = { "LabelSubText" },
                Margin = new Thickness(0, 4, 0, 0),
            });
        }
        favoritePanel.AddChild(favoriteSection);
        contentContainer.AddChild(favoritePanel);
    }

    private void BuildAllEmojisSection(BoxContainer contentContainer)
    {
        var allPanel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = CreateEmojiPanelStyle(),
        };

        var allSection = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        allSection.AddChild(new Label
        {
            Text = _loc.GetString("messenger-emoji-all-title"),
            StyleClasses = { "Bold" },
            Margin = new Thickness(0, 0, 0, 4),
        });

        var allEmojisContainer = new GridContainer
        {
            Columns = 5,
            HorizontalExpand = true,
        };

        foreach (var emoji in _emoji.Emojis.Values)
        {
            allEmojisContainer.AddChild(CreateEmojiButton(emoji));
        }

        allSection.AddChild(allEmojisContainer);
        allPanel.AddChild(allSection);
        contentContainer.AddChild(allPanel);
    }

    private Button CreateEmojiButton(EmojiPrototype emoji)
    {
        var emojiButton = new Button
        {
            MinSize = new Vector2(75, 75),
            MaxSize = new Vector2(75, 75),
            ToolTip = emoji.Code,
        };

        var spriteSpec = new SpriteSpecifier.Rsi(new ResPath(emoji.SpritePath), emoji.SpriteState);
        var state = _sprite.RsiStateLike(spriteSpec);

        emojiButton.Label.Visible = false;

        if (state.IsAnimated)
        {
            var animatedRect = new AnimatedTextureRect
            {
                SetWidth = 65,
                SetHeight = 65,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            };
            animatedRect.SetFromSpriteSpecifier(spriteSpec);
            animatedRect.DisplayRect.HorizontalExpand = true;
            animatedRect.DisplayRect.VerticalExpand = true;
            animatedRect.DisplayRect.Stretch = TextureRect.StretchMode.KeepAspectCentered;
            emojiButton.AddChild(animatedRect);
        }
        else
        {
            var texture = _sprite.Frame0(spriteSpec);
            var textureRect = new TextureRect
            {
                Texture = texture,
                SetWidth = 45,
                SetHeight = 45,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
            };
            emojiButton.AddChild(textureRect);
        }

        var emojiCode = emoji.Code;

        emojiButton.OnPressed += _ =>
        {
            OnEmojiSelected?.Invoke(emojiCode);
            AddToRecentEmojis(emojiCode);
            UpdateEmojiPickerContent();
        };

        emojiButton.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.UIRightClick)
            {
                args.Handle();
                ToggleFavoriteEmoji(emojiCode);
                UpdateEmojiPickerContent();
            }
        };

        return emojiButton;
    }

    private void AddToRecentEmojis(string emojiCode)
    {
        _recentEmojis.Remove(emojiCode);
        _recentEmojis.Add(emojiCode);

        if (_recentEmojis.Count > 5)
            _recentEmojis.RemoveAt(0);

        SaveRecentEmojis();
    }

    private void ToggleFavoriteEmoji(string emojiCode)
    {
        if (!_emoji.Emojis.TryGetValue(emojiCode, out var proto))
            return;

        if (!_favoriteEmojis.Add(proto))
            _favoriteEmojis.Remove(proto);

        SaveFavoriteEmojis();
    }

    private void LoadSavedEmojis()
    {
        LoadRecentEmojis();
        LoadFavoriteEmojis();
    }

    private void LoadRecentEmojis()
    {
        var recentEmojisStr = _cfg.GetCVar(SunriseCCVars.MessengerRecentEmojis);
        if (string.IsNullOrWhiteSpace(recentEmojisStr))
            return;

        var emojiCodes = recentEmojisStr.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _recentEmojis.Clear();

        foreach (var code in emojiCodes)
        {
            if (string.IsNullOrWhiteSpace(code))
                continue;

            if (!_emoji.Emojis.ContainsKey(code))
                continue;

            _recentEmojis.Add(code);
            if (_recentEmojis.Count >= 5)
                break;
        }
    }

    private void LoadFavoriteEmojis()
    {
        var favoriteEmojisStr = _cfg.GetCVar(SunriseCCVars.MessengerFavoriteEmojis);
        if (string.IsNullOrWhiteSpace(favoriteEmojisStr))
            return;

        var emojiCodes = favoriteEmojisStr.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _favoriteEmojis.Clear();

        foreach (var code in emojiCodes)
        {
            if (string.IsNullOrWhiteSpace(code))
                continue;

            if (!_emoji.Emojis.TryGetValue(code, out var proto))
                continue;

            _favoriteEmojis.Add(proto);
        }
    }

    private void SaveRecentEmojis()
    {
        var emojiCodesStr = string.Join(",", _recentEmojis);
        _cfg.SetCVar(SunriseCCVars.MessengerRecentEmojis, emojiCodesStr);
        _cfg.SaveToFile();
    }

    private void SaveFavoriteEmojis()
    {
        var emojiCodesStr = string.Join(",", _favoriteEmojis.Select(e => e.Code));
        _cfg.SetCVar(SunriseCCVars.MessengerFavoriteEmojis, emojiCodesStr);
        _cfg.SaveToFile();
    }
}

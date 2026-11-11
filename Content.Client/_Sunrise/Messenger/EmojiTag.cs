using System.Diagnostics.CodeAnalysis;
using Content.Shared._Sunrise.Messenger;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Messenger;

/// <summary>
/// Тег для отображения эмодзи мессенджера в RichText.
/// Разрешает только эмодзи из прототипов, чтобы игроки не могли использовать произвольные текстуры.
/// </summary>
public sealed partial class EmojiTag : IMarkupTagHandler
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    private static SpriteSystem? _spriteSystem;

    public string Name => "emoji";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (!node.Attributes.TryGetValue("id", out var rawId) || !rawId.TryGetString(out var emojiId))
            return false;

        if (!_prototypeManager.TryIndex<EmojiPrototype>(emojiId, out var emoji))
            return false;

        try
        {
            var spriteSpec = new SpriteSpecifier.Rsi(new ResPath(emoji.SpritePath), emoji.SpriteState);

            _spriteSystem ??= _entitySystemManager.GetEntitySystem<SpriteSystem>();
            var state = _spriteSystem.RsiStateLike(spriteSpec);

            if (state.IsAnimated)
            {
                var animatedRect = new AnimatedTextureRect
                {
                    MinWidth = 50,
                    MinHeight = 50,
                    HorizontalAlignment = Control.HAlignment.Stretch,
                    VerticalAlignment = Control.VAlignment.Stretch,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                };
                animatedRect.SetFromSpriteSpecifier(spriteSpec);
                animatedRect.DisplayRect.HorizontalExpand = true;
                animatedRect.DisplayRect.VerticalExpand = true;
                animatedRect.DisplayRect.Stretch = TextureRect.StretchMode.KeepAspectCentered;
                control = animatedRect;
            }
            else
            {
                var texture = _spriteSystem.Frame0(spriteSpec);
                var textureRect = new TextureRect
                {
                    Texture = texture,
                    MinWidth = 50,
                    MinHeight = 50,
                    HorizontalAlignment = Control.HAlignment.Stretch,
                    VerticalAlignment = Control.VAlignment.Stretch,
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                };
                control = textureRect;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}

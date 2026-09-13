using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client._Sunrise.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.UserInterface.RichText;

public abstract partial class BaseTextureTag : IMarkupTagHandler
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    private static SpriteSystem? _spriteSystem;

    public virtual string Name => "example";

    public abstract bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control);

    protected bool TryDrawIcon(string path, long scaleValue, [NotNullWhen(true)] out Control? control)
    {
        var texture = new TextureRect();

        SplitRsiPath(path, out var rsiPath, out var state);
        var resourceCache = new SpriteSpecifier.Rsi(new ResPath(rsiPath), state);

        _spriteSystem ??= _entitySystemManager.GetEntitySystem<SpriteSystem>();
        texture.Texture = _spriteSystem.Frame0(resourceCache);
        texture.TextureScale = new Vector2(scaleValue, scaleValue);

        control = texture;
        return true;
    }

    protected bool TryDrawIcon(EntProtoId entProtoId, long scaleValue, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        var texture = new TextureRect();

        if (!_prototypeManager.TryIndex(entProtoId, out var prototype))
            return false;

        _spriteSystem ??= _entitySystemManager.GetEntitySystem<SpriteSystem>();
        texture.Texture = _spriteSystem.Frame0(prototype);
        texture.TextureScale = new Vector2(scaleValue, scaleValue);

        control = texture;
        return true;
    }

    protected static bool TryDrawIconEntity(string stringUid, long scaleValue, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (!EntityUid.TryParse(stringUid, out var entityUid))
            return false;

        var spriteView = new SunriseStaticSpriteView(entityUid)
        {
            SetSize = new Vector2(48f, 32f),
            Scale = new Vector2(scaleValue, scaleValue),
        };

        control = spriteView;
        return true;
    }

    /// <summary>
    /// Очищает строку от мусора, который приходит вместе с ней.
    /// Используется для нестандартных обработчиков.
    /// </summary>
    /// <remarks>
    /// Перед тем, как использовать это для MarkupParameter убедитесь, что у него нет нужных вам встроенных функций парсинга.
    /// </remarks>
    protected static string ClearString(string str)
    {
        str = str.Replace("=", "");
        str = str.Replace("\"", "");
        str = str.Trim();

        return str;
    }

    protected static void SplitRsiPath(string fullPath, out string rsiPath, out string state)
    {
        var lastSlash = fullPath.LastIndexOf('/');
        var lastDot = fullPath.LastIndexOf('.');
        if (lastSlash == -1 || lastDot == -1 || lastDot < lastSlash)
        {
            rsiPath = fullPath;
            state = string.Empty;
            return;
        }
        rsiPath = fullPath.Substring(0, lastSlash);
        state = fullPath.Substring(lastSlash + 1, lastDot - lastSlash - 1);
    }
}

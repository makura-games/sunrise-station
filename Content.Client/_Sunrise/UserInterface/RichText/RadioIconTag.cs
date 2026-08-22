using System.Diagnostics.CodeAnalysis;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.UserInterface.RichText;

public sealed partial class RadioIconTag : BaseTextureTag
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceCache _cache = default!;

    private static FontResource? _font;

    public override string Name => "radicon";

    public override bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (_cfg.GetCVar(SunriseCCVars.ChatIconsEnabled))
        {
            if (!node.Attributes.TryGetValue("path", out var rawPath) || !rawPath.TryGetString(out var path))
                return false;

            if (!node.Attributes.TryGetValue("scale", out var scale) || !scale.TryGetLong(out var scaleValue))
                scaleValue = 1;

            if (!TryDrawIcon(path, scaleValue.Value, out var texture))
                return false;

            control = texture;
        }
        else
        {
            if (!node.Attributes.TryGetValue("text", out var text) || !text.TryGetString(out var textValue))
                return false;

            if (!node.Attributes.TryGetValue("color", out var rawColor)|| !rawColor.TryGetString(out var colorText))
                return false;

            control = DrawText(textValue, colorText);
        }

        return true;
    }

    private Label DrawText(string text, string color)
    {
        _font ??= _cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf");

        var label = new Label
        {
            Text = text,
            FontColorOverride = Color.FromHex(color),
            FontOverride = new VectorFont(_font, 13),
        };

        return label;
    }
}

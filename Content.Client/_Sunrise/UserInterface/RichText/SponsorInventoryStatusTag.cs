using System.Diagnostics.CodeAnalysis;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.UserInterface.RichText;

public sealed partial class SponsorInventoryStatusTag : IMarkupTagHandler
{
    private const int MarkerFontSize = 13;
    private const int MarkerWidth = 16;
    private const string GoodType = "good";
    private const string BadType = "bad";
    private const string WarningType = "warning";

    private static readonly string[] MarkerFontPaths =
    [
        "/Fonts/NotoSans/NotoSans-Bold.ttf",
        "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
        "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
    ];

    [Dependency] private IResourceCache _cache = default!;

    private Font? _markerFont;

    public string Name => "sinvstatus";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (node.Closing)
        {
            control = null;
            return false;
        }

        var statusType = GoodType;
        if (node.Attributes.TryGetValue("type", out var rawType) &&
            rawType.TryGetString(out var type))
        {
            statusType = type;
        }

        var color = GetDefaultColor(statusType);
        if (node.Attributes.TryGetValue("color", out var rawColor) &&
            rawColor.TryGetString(out var colorText))
        {
            color = Color.FromHex(colorText, color);
        }

        control = new Label
        {
            Text = GetMarker(statusType),
            MinWidth = MarkerWidth,
            Align = Label.AlignMode.Center,
            VAlign = Label.VAlignMode.Center,
            FontOverride = GetMarkerFont(),
            FontColorOverride = color,
        };

        return true;
    }

    private Font GetMarkerFont()
    {
        return _markerFont ??= _cache.GetFont(MarkerFontPaths, MarkerFontSize);
    }

    private static string GetMarker(string statusType)
    {
        return statusType switch
        {
            BadType => "\u2715",
            WarningType => "\u26a0",
            _ => "\u2713",
        };
    }

    private static Color GetDefaultColor(string statusType)
    {
        return statusType switch
        {
            BadType => Color.FromHex("#e45f5f"),
            WarningType => Color.FromHex("#d7b45f"),
            _ => Color.FromHex("#72d979"),
        };
    }
}

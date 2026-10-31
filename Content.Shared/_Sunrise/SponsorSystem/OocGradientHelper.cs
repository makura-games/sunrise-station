using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.SponsorSystem;

public static class OocGradientHelper
{
    public static IEnumerable<string> AllGradientIds => GetAllGradientIds();

    private static List<Color> GetPrototypeColors(SponsorOocColorPrototype proto)
    {
        if (proto.Colors.Count > 0)
            return proto.Colors;

        if (proto.Color != null)
            return new List<Color> { proto.Color.Value };

        return new List<Color>();
    }

    public static bool IsGradientId(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        if (protoManager.TryIndex<SponsorOocColorPrototype>(value, out var proto))
        {
            return GetPrototypeColors(proto).Count > 1;
        }
        return false;
    }

    public static bool TryGetGradientColors(string gradientId, [NotNullWhen(true)] out Color[]? colors)
    {
        colors = null;
        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        if (protoManager.TryIndex<SponsorOocColorPrototype>(gradientId, out var proto))
        {
            var list = GetPrototypeColors(proto);
            if (list.Count > 1)
            {
                colors = list.ToArray();
                return true;
            }
        }
        return false;
    }

    private static List<string> GetAllGradientIds()
    {
        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        var list = new List<string>();
        foreach (var proto in protoManager.EnumeratePrototypes<SponsorOocColorPrototype>())
        {
            if (GetPrototypeColors(proto).Count > 1)
            {
                list.Add(proto.ID);
            }
        }
        return list;
    }

    /// <summary>
    /// Tries to resolve a visible OOC title by its SponsorOocTitlePrototype ID.
    /// </summary>
    public static bool TryResolveTitle(string titleOrProtoId, [NotNullWhen(true)] out string? title)
    {
        title = null;
        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        if (protoManager.TryIndex<SponsorOocTitlePrototype>(titleOrProtoId, out var titleProto))
        {
            title = titleProto.Title;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to resolve an OOC color by its SponsorOocColorPrototype ID.
    /// </summary>
    public static bool TryResolveColor(string colorOrProtoId, [NotNullWhen(true)] out Color? color)
    {
        color = null;
        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        if (protoManager.TryIndex<SponsorOocColorPrototype>(colorOrProtoId, out var proto))
        {
            var list = GetPrototypeColors(proto);
            if (list.Count > 0)
            {
                color = list[0];
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Applies a gradient from an array of colors to the passed text, forming markup.
    /// </summary>
    public static string ApplyGradient(string text, Color[] colors)
    {
        if (text.Length == 0)
            return text;
        if (colors.Length == 1)
            return $"[color=#{colors[0].ToHexNoAlpha().TrimStart('#')}]{text.Replace("\\", "\\\\").Replace("[", "\\[")}[/color]"; // Sunrise-Edit

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            float ratio = (float)i / (text.Length - 1 == 0 ? 1 : text.Length - 1);
            float segment = ratio * (colors.Length - 1);
            int idx = (int)Math.Floor(segment);
            float t = segment - idx;

            if (idx >= colors.Length - 1)
            {
                idx = colors.Length - 2;
                t = 1.0f;
            }

            var startColor = colors[idx];
            var endColor = colors[idx + 1];

            // Интерполируем RGB-компоненты между двумя соседними цветами градиента на основе шага t
            var r = startColor.R + (endColor.R - startColor.R) * t;
            var g = startColor.G + (endColor.G - startColor.G) * t;
            var b = startColor.B + (endColor.B - startColor.B) * t;
            var color = new Color(r, g, b);

            var c = text[i];
            var escapedChar = c switch
            {
                '\\' => "\\\\",
                '[' => "\\[",
                _ => c.ToString()
            };
            sb.Append($"[color=#{color.ToHexNoAlpha().TrimStart('#')}]{escapedChar}[/color]");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Applies a gradient to text by its SponsorOocColorPrototype ID.
    /// </summary>
    public static string ApplyGradientById(string text, string gradientId)
    {
        if (TryGetGradientColors(gradientId, out var colors))
        {
            return ApplyGradient(text, colors);
        }
        return text;
    }
}

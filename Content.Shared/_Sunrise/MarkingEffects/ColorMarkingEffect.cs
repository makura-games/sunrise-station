using System.Linq;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.MarkingEffects;

[Serializable, NetSerializable]
public sealed partial class ColorMarkingEffect : MarkingEffect
{
    public override MarkingEffectType Type => MarkingEffectType.Color;

    public Color GetColor()
        => Colors.TryGetValue("base", out var col) ? col : Color.White;

    #region Constructors

    public ColorMarkingEffect(Color color) : base(color) { }
    public static readonly ColorMarkingEffect White = new (Color.White);

    #endregion

    #region Parsers

    public override string ToString()
    {
        Dictionary<string, string> dict = new();

        var color = GetColor();
        dict.Add("color.base", color.ToHex());

        var result = string.Join(",", dict.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"{Type.ToString()}=={result}";
    }

    public static ColorMarkingEffect? Parse(Dictionary<string, string> dict)
    {
        var color = Color.White;

        foreach (var (type, value) in dict)
        {
            if (type == "color.base")
                color = Color.TryFromHex(value, out var parsedColor) ? parsedColor : Color.White;
        }

        return new ColorMarkingEffect(color);
    }

    #endregion

    #region Other methods

    public override ColorMarkingEffect Clone()
    {
        if (!Colors.TryGetValue("base", out var color))
            color = Color.White;

        return new ColorMarkingEffect(color);
    }

    public override bool Equals(MarkingEffect? maybeOther)
    {
        if (maybeOther is not ColorMarkingEffect other)
            return false;

        return DictionaryEquals(Colors, other.Colors);
    }

    #endregion
}

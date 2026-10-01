using System.Linq;
using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.MarkingEffects;

[Serializable, NetSerializable]
public sealed partial class GradientMarkingEffect : MarkingEffect
{
    public override MarkingEffectType Type => MarkingEffectType.Gradient;

    public Vector2 Offset = new(0, -190/100f);
    public Vector2 Size = new(1, 33/100f);
    public float Rotation = 0;
    public float Speed = 1;
    public bool Pixelated = true;
    public bool Mirrored = false;

    #region Parsing
    public override string ToString()
    {
        var dict = new Dictionary<string, string>();

        dict.Add("offset", ParamToString(Offset));
        dict.Add("size", ParamToString(Size));
        dict.Add("rotation", ParamToString(Rotation));
        dict.Add("speed", ParamToString(Speed));
        dict.Add("pixelated", ParamToString(Pixelated));
        dict.Add("mirrored", ParamToString(Mirrored));

        foreach (var (k, v) in Colors)
            dict.Add($"color.{k}", $"{v.ToHex()}");

        var result = string.Join(",", dict.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"{Type.ToString()}=={result}";
    }

    public static GradientMarkingEffect? Parse(Dictionary<string, string> dict)
    {
        var colors = new Dictionary<string, Color>();

        var offset = new Vector2(0, -190/100f);
        var size = new Vector2(1, 33/100f);
        var rotation = 0f;
        var speed = 1f;
        var pixelated = true;
        var mirrored = false;

        foreach (var (type, value) in dict)
        {
            switch (type)
            {
                case "offset":
                    TryParseParam(value, out offset);
                    break;
                case "size":
                    TryParseParam(value, out size);
                    break;
                case "rotation":
                    TryParseParam(value, out rotation);
                    break;
                case "speed":
                    TryParseParam(value, out speed);
                    break;
                case "pixelated":
                    TryParseParam(value, out pixelated);
                    break;
                case "mirrored":
                    TryParseParam(value, out mirrored);
                    break;
                default:
                {
                    if (type.StartsWith("color."))
                    {
                        colors[type["color.".Length..]] = Color.TryFromHex(value, out var parsedColor)
                            ? parsedColor
                            : Color.White;
                    }
                    break;
                }
            }
        }

        // Ensure both base and gradient colors are present for compatibility
        if (!colors.ContainsKey("base"))
            colors["base"] = Color.White;
        if (!colors.ContainsKey("gradient"))
            colors["gradient"] = colors["base"]; // Use base color as fallback

        return new GradientMarkingEffect(colors, offset, size, rotation, speed, pixelated, mirrored);
    }
    #endregion

    #region Constructors
    public GradientMarkingEffect()
    {
        Colors = new Dictionary<string, Color>()
        {
            { "base", Color.White },
            { "gradient", Color.White }
        };
    }

    public GradientMarkingEffect(Color color)
    {
        Colors = new Dictionary<string, Color>
        {
            {"base", color },
            {"gradient", color }
        };
    }

    public GradientMarkingEffect(Dictionary<string, Color> colors,
        Vector2 offset,
        Vector2 size,
        float rotation,
        float speed,
        bool pixelated,
        bool mirrored)
    {
        Colors = colors;
        Offset = offset;
        Size = size;
        Rotation = rotation;
        Speed = speed;
        Pixelated = pixelated;
        Mirrored = mirrored;
    }
    #endregion

    #region Other methods
    public override GradientMarkingEffect Clone()
    {
        return new GradientMarkingEffect(new(Colors), new(Offset.X, Offset.Y), new(Size.X, Size.Y), Rotation, Speed, Pixelated, Mirrored);
    }

    public override bool Equals(MarkingEffect? maybeOther)
    {
        if (maybeOther is not GradientMarkingEffect other)
            return false;

        return DictionaryEquals(Colors, other.Colors)
               && Offset.Equals(other.Offset)
               && Size.Equals(other.Size)
               && Rotation.Equals(other.Rotation)
               && Speed.Equals(other.Speed)
               && Pixelated.Equals(other.Pixelated)
               && Mirrored.Equals(other.Mirrored);
    }
    #endregion
}

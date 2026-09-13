using System;

namespace Content.Shared._Sunrise.MarkingEffects;

public static class MarkingEffectCompatibility
{
    public static MarkingEffect WithLegacyBaseColor(MarkingEffect effect, Color baseColor)
    {
        var clone = effect.Clone();

        if (clone is ColorMarkingEffect)
            return new ColorMarkingEffect(baseColor);

        clone.Colors["base"] = baseColor;

        if (clone is GradientMarkingEffect or RoughGradientMarkingEffect)
            clone.Colors.TryAdd("gradient", Color.White);

        return clone;
    }

    public static MarkingEffect CreateFromType(MarkingEffectType type, Color baseColor)
    {
        MarkingEffect effect = type switch
        {
            MarkingEffectType.Gradient => new GradientMarkingEffect(),
            MarkingEffectType.RoughGradient => new RoughGradientMarkingEffect(),
            _ => new ColorMarkingEffect(baseColor),
        };

        return WithLegacyBaseColor(effect, baseColor);
    }

    public static bool TryReadLegacyEffect(
        int rawType,
        string? serialized,
        Color baseColor,
        out MarkingEffect effect)
    {
        var type = Enum.IsDefined(typeof(MarkingEffectType), rawType)
            ? (MarkingEffectType)rawType
            : MarkingEffectType.Color;

        return TryReadLegacyEffect(type, serialized, baseColor, out effect);
    }

    public static bool TryReadLegacyEffect(
        MarkingEffectType type,
        string? serialized,
        Color baseColor,
        out MarkingEffect effect)
    {
        if (!string.IsNullOrWhiteSpace(serialized))
        {
            if (MarkingEffect.Parse(serialized) is { } parsed)
            {
                if (parsed is ColorMarkingEffect)
                {
                    effect = new ColorMarkingEffect(baseColor);
                    return false;
                }

                effect = WithLegacyBaseColor(parsed, baseColor);
                return true;
            }

            if (Color.TryFromHex(serialized) is { } gradientColor)
            {
                var gradient = new GradientMarkingEffect();
                gradient.Colors["base"] = baseColor;
                gradient.Colors["gradient"] = gradientColor;
                effect = gradient;
                return true;
            }
        }

        if (type != MarkingEffectType.Color)
        {
            effect = CreateFromType(type, baseColor);
            return true;
        }

        effect = new ColorMarkingEffect(baseColor);
        return false;
    }
}

using Content.Client.Stylesheets.Palette;
using Content.Shared._Sunrise.Disease.Microscope;

namespace Content.Client._Sunrise.Disease.Microscope;

/// <summary>Постоянные цвета колоний и инструментов, общие для окуляра, генома и подсказок.</summary>
public static class DiseaseMicroscopePalette
{
    // Принадлежность колонии читается только по цвету: зелёная — ваша, красная — конкурента.
    private static readonly ColorPalette TargetColony = ColorPalette.FromHexBase("#5fcf7a");
    private static readonly ColorPalette RivalColony = ColorPalette.FromHexBase("#e05a4e");

    /// <summary>Цвет колонии: им одним и различаются стороны партии.</summary>
    public static Color Colony(byte colony) => colony switch
    {
        DiseaseCultureBoard.Target => TargetColony.Base,
        DiseaseCultureBoard.Opponent => RivalColony.Base,
        _ => Palettes.Slate.Text,
    };

    /// <summary>Цвет уровня сложности: от спокойной партии к безнадёжной.</summary>
    public static Color Tier(byte tier) => tier switch
    {
        <= 1 => Palettes.Green.Base,
        2 => Palettes.Green.Base,
        3 => Palettes.Amber.Base,
        _ => Palettes.Red.Base,
    };

    /// <summary>Читаемая подсветка материала, который будет удалён.</summary>
    public static Color Removed => RivalColony.Base;

    /// <summary>Цвет выжженной ингибитором среды.</summary>
    public static Color Sterile => Palettes.Slate.Text;

    /// <summary>Нейтральные обводки, курсор и разметка поля.</summary>
    public static Color Marking => Palettes.AlphaModulate.Base;
}

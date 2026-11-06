// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt
using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.GhostTheme;

[Prototype]
public sealed partial class GhostThemePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name")]
    public string Name { get; private set; } = string.Empty;

    [DataField("scale")]
    public Vector2 Scale { get; private set; } = new(1, 1);

    [DataField("color")]
    public Color SpriteColor = Color.White;

    [DataField("sprite", required: true)]
    public SpriteSpecifier Sprite { get; private set; } = default!;

    /// <summary>
    /// Оставляет базовый слой призрака под управлением апстримной системы причины смерти.
    /// </summary>
    [DataField]
    public bool UseUpstreamSprite;

    [DataField]
    public bool SponsorOnly;
}

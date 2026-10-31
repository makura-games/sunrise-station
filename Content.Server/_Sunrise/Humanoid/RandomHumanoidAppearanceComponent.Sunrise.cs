using Content.Server.Humanoid.Components;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Humanoid.Components;

public sealed partial class RandomHumanoidAppearanceComponent
{
    /// <summary>
    /// After randomizing, sets the hair style to this, if possible
    /// </summary>
    [DataField]
    public string? Hair;

    // Sunrise-Start
    /// <summary>
    /// Настраивает цвет кожи в HEX формате. Учитывайте что только бежевые и темные цвета могут подойти человекоподобным. Советую подбирать цвет прям в игре.
    /// </summary>
    [DataField]
    public Color? SkinColor;

    /// <summary>
    /// Настраивает цвет волос в HEX формате
    /// </summary>
    [DataField]
    public Color? HairColor;

    /// <summary>
    /// Настраивает цвет бороды в HEX формате
    /// </summary>
    [DataField]
    public Color? FacialHairColor;
}

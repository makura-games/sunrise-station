// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt;

namespace Content.Server._Sunrise.HairDye;

/// <summary>
/// Компонент используется для краски для волос.
/// </summary>
[RegisterComponent]
public sealed partial class HairDyerComponent : Component
{
    [DataField(required: true)]
    public Color TargetColor;

    [DataField]
    public bool Mode;  // false - волосы true - борода
}

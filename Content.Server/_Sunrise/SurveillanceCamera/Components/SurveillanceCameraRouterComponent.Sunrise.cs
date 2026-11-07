#pragma warning disable IDE0130 // Пространство имён vanilla-класса сохраняется для partial-расширения.
namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraRouterComponent
{
    /// <summary>
    /// Цвет подсети для совместимости с Sunrise-прототипами маршрутизаторов камер.
    /// </summary>
    [DataField]
    public Color SubnetColor;
}

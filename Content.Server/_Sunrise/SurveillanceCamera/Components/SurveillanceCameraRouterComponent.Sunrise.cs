// ReSharper disable once CheckNamespace
namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraRouterComponent
{
    /// <summary>
    /// Цвет подсети для совместимости с Sunrise-прототипами маршрутизаторов камер.
    /// </summary>
    [DataField]
    public Color SubnetColor;
}

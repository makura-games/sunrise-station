#pragma warning disable IDE0130 // Пространство имён совпадает с расширяемой vanilla-подсистемой.
namespace Content.Client.Parallax.Data;

public sealed partial class ParallaxLayerConfig
{
    /// <summary>
    /// Rotation of the layer around its center, in degrees.
    /// </summary>
    [DataField]
    public Angle Rotation { get; set; }
}

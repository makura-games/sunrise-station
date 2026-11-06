#pragma warning disable IDE0130 // Используем пространство имён оверлея Wizden.
namespace Content.Client.Overlays;

public sealed partial class NightVisionOverlay
{
    protected override void DisposeBehavior()
    {
        _nightVisionShader.Dispose();
        base.DisposeBehavior();
    }
}

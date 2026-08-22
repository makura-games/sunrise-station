using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Sunrise.Shaders.Bloom;

/// <summary>
/// Collects compatible lights and supplies their state to the bloom overlays.
/// </summary>
public sealed partial class LightingOverlaySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private BloomOverlayTreeSystem _bloomTree = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private TransformSystem _transform = default!;

    [Dependency] private EntityQuery<PointLightComponent> _pointLightQuery = default!;
    private PointLightingOverlay? _bloomOverlay;
    private float _bloomStrength = 0.7f;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configuration, SunriseCCVars.LightBloomEnabled, OnBloomEnabledChanged, true);
        Subs.CVar(_configuration, SunriseCCVars.LightBloomStrength, OnBloomStrengthChanged, true);
    }

    public override void Shutdown()
    {
        if (_bloomOverlay is { } overlay)
        {
            if (_overlay.HasOverlay<PointLightingOverlay>())
                _overlay.RemoveOverlay(overlay);

            overlay.Dispose();
            _bloomOverlay = null;
        }

        base.Shutdown();
    }

    private void OnBloomEnabledChanged(bool isEnabled)
    {
        if (!isEnabled)
        {
            if (_bloomOverlay is not { } overlay)
                return;

            if (_overlay.HasOverlay<PointLightingOverlay>())
                _overlay.RemoveOverlay(overlay);

            overlay.Dispose();
            _bloomOverlay = null;
            return;
        }

        if (_overlay.HasOverlay<PointLightingOverlay>())
            return;

        _bloomOverlay ??= new PointLightingOverlay(
            _bloomTree,
            _prototype,
            _sprite,
            _transform,
            _pointLightQuery,
            (int) DrawDepth.Effects,
            0.8f,
            0.05f,
            _bloomStrength);

        _overlay.AddOverlay(_bloomOverlay);
    }

    private void OnBloomStrengthChanged(float strength)
    {
        _bloomStrength = Math.Clamp(strength, 0f, 1f);
        if (_bloomOverlay is { } overlay)
            overlay.BloomStrength = _bloomStrength;
    }
}

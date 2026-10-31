using Content.Client._Sunrise.Sandbox.Transparency.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Sandbox.Transparency.Overlays;

/// <summary>
/// Applies temporary transparency to anchored sprites while mapper transparency mode is active.
/// </summary>
public sealed partial class MappingTransparencyOverlay : Overlay
{
    [Dependency] private IEntityManager _ent = default!;
    [Dependency] private IEyeManager _eye = default!;

    private readonly EntityLookupSystem _entityLookup;
    private readonly SpriteSystem _sprite;

    private readonly List<(Entity<SpriteComponent> ent, float BaseAlpha)> _cachedBaseAlphas = new(256);

    /// <inheritdoc />
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    /// <summary>
    /// Gets or sets the opacity reduction percentage applied to anchored sprites.
    /// </summary>
    public int TransparencyPercent { get; set; } = MappingTransparencySystem.DefaultTransparencyPercent;

    /// <summary>
    /// Creates the overlay and resolves the sprite system it mutates each frame.
    /// </summary>
    public MappingTransparencyOverlay()
    {
        IoCManager.InjectDependencies(this);
        _entityLookup = _ent.System<EntityLookupSystem>();
        _sprite = _ent.System<SpriteSystem>();
    }

    /// <summary>
    /// Restores the original alpha values for any sprites changed by the overlay.
    /// </summary>
    public void ResetTransparency()
    {
        RestoreCachedTransparency();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        RestoreCachedTransparency();
        ApplyTransparency();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
    }

    private void ApplyTransparency()
    {
        var currentMapId = _eye.CurrentEye.Position.MapId;
        var worldViewport = _eye.GetWorldViewport();
        var query = _ent.AllEntityQueryEnumerator<SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sprite, out var xform))
        {
            if (!xform.Anchored || xform.MapID != currentMapId)
                continue;

            if (!_entityLookup.GetWorldAABB(uid, xform).Intersects(worldViewport))
                continue;

            ApplyTransparency((uid, sprite));
        }
    }

    private void RestoreCachedTransparency()
    {
        foreach (var (ent, baseAlpha) in _cachedBaseAlphas)
        {
            RestoreTransparency(ent, baseAlpha);
        }

        _cachedBaseAlphas.Clear();
    }

    private void ApplyTransparency(Entity<SpriteComponent> ent)
    {
        var targetAlpha = ent.Comp.Color.A * (1f - TransparencyPercent / 100f);
        if (MathHelper.CloseTo(ent.Comp.Color.A, targetAlpha))
            return;

        _cachedBaseAlphas.Add((ent, ent.Comp.Color.A));
        _sprite.SetColor(ent.AsNullable(), ent.Comp.Color.WithAlpha(targetAlpha));
    }

    private void RestoreTransparency(Entity<SpriteComponent> ent, float baseAlpha)
    {
        if (MathHelper.CloseTo(ent.Comp.Color.A, baseAlpha))
            return;

        _sprite.SetColor(ent.AsNullable(), ent.Comp.Color.WithAlpha(baseAlpha));
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();

        RestoreCachedTransparency();
    }
}

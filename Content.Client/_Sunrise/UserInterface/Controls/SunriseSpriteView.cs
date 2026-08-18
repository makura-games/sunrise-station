using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.UserInterface.Controls;

[Virtual]
public class SunriseStaticSpriteView : Control
{
    private readonly SpriteSystem _sprite;
    private readonly IEntityManager _entityMan;

    private SpriteComponent? _cachedSprite;
    private EntityUid? _sourceEntity;
    private EntityUid? _previewEntity;
    private Angle _rotation;

    [ViewVariables]
    private Entity<SpriteComponent> Entity { get; set; }

    /// <summary>
    /// This field configures automatic scaling of the sprite. This automatic scaling is done before
    /// applying the explicitly set scale <see cref="SunriseStaticSpriteView.Scale"/>.
    /// </summary>
    public StretchMode Stretch  { get; set; } = StretchMode.Fit;

    public enum StretchMode
    {
        /// <summary>
        /// Don't automatically scale the sprite. The sprite can still be scaled via <see cref="SunriseStaticSpriteView.Scale"/>
        /// </summary>
        None,

        /// <summary>
        /// Scales the sprite down so that it fits within the control. Does not scale the sprite up. Keeps the same
        /// aspect ratio. This automatic scaling is done before applying <see cref="SunriseStaticSpriteView.Scale"/>.
        /// </summary>
        Fit,

        /// <summary>
        /// Scale the sprite up or down so that it fills the whole control. Keeps the same aspect ratio. This
        /// automatic scaling is done before applying <see cref="SunriseStaticSpriteView.Scale"/>.
        /// </summary>
        Fill
    }

    /// <summary>
    /// Overrides the direction used to render the sprite.
    /// </summary>
    /// <remarks>
    /// If null, the world space orientation of the entity will be used. Otherwise the specified direction will be
    /// used.
    /// </remarks>
    public Direction? OverrideDirection { get; set; }

    #region Transform

    private Vector2 _scale = Vector2.One;

    /// <summary>
    /// Scale to apply when rendering the sprite. This is separate from the sprite's scale.
    /// </summary>
    public Vector2 Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Cached desired size. Differs from <see cref="Control.DesiredSize"/> as it it is not clamped by the
    /// minimum/maximum size options.
    /// </summary>
    private Vector2 _spriteSize;

    /// <summary>
    /// Determines whether or not the sprite's offset be applied to the control.
    /// </summary>
    public bool SpriteOffset { get; set; }

    #endregion

    public SunriseStaticSpriteView(EntityUid uid)
    {
        IoCManager.Resolve(ref _entityMan);

        RectClipContent = true;

        _sprite = _entityMan.System<SpriteSystem>();

        SetEntity(uid);
    }

    public void SetEntity(EntityUid uid)
    {
        if (_sourceEntity == uid &&
            _previewEntity is { } preview &&
            !_entityMan.Deleted(preview))
        {
            return;
        }

        _sourceEntity = uid;

        if (IsInsideTree)
            RefreshPreviewEntity();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        RefreshPreviewEntity();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        ClearPreviewEntity(false);
    }

    private void RefreshPreviewEntity()
    {
        if (_sourceEntity is not { } uid)
            return;

        if (!_entityMan.TryGetComponent(uid, out SpriteComponent? sprite))
        {
            ClearPreviewEntity(true);
            return;
        }

        if (_previewEntity == null || _entityMan.Deleted(_previewEntity))
            _previewEntity = _entityMan.SpawnEntity("hoverentity", MapCoordinates.Nullspace);

        if (!_entityMan.TryGetComponent(_previewEntity, out SpriteComponent? previewSprite))
        {
            ClearPreviewEntity(true);
            return;
        }

        // Создаем глубокую копию спрайта на отдельной клиентской сущности.
        _sprite.CopySprite((uid, sprite), (_previewEntity.Value, previewSprite));

        _rotation = sprite.Rotation;

        _cachedSprite = previewSprite;
        Entity = (_previewEntity.Value, previewSprite);
        InvalidateMeasure();
    }

    private void ClearPreviewEntity(bool clearSource)
    {
        _entityMan.TryQueueDeleteEntity(_previewEntity);
        _previewEntity = null;
        _cachedSprite = null;
        Entity = default;

        if (clearSource)
            _sourceEntity = null;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // TODO Make this get called when sprite bounds/properties update?
        UpdateSize();
        return _spriteSize;
    }

    private void UpdateSize()
    {
        if (!ResolveEntity(out var uid, out var sprite))
            return;

        var spriteBox = _sprite.CalculateBounds((uid, sprite), default, _rotation, _rotation)
            .CalcBoundingBox();

        if (!SpriteOffset)
        {
            // re-center the box.
            spriteBox = spriteBox.Translated(-spriteBox.Center);
        }

        // Scale the box (including any offset);
        var scale = _scale * EyeManager.PixelsPerMeter;
        var bl = spriteBox.BottomLeft * scale;
        var tr = spriteBox.TopRight * scale;

        // This view will be centered on (0,0). If the sprite was shifted by (1,2) the actual size of the control
        // would need to be at least (2,4).
        tr = Vector2.Max(tr, Vector2.Zero);
        bl = Vector2.Min(bl, Vector2.Zero);
        tr = Vector2.Max(tr, -bl);
        bl = Vector2.Min(bl, -tr);
        var box = new Box2(bl, tr);

        DebugTools.Assert(box.Contains(Vector2.Zero));
        DebugTools.Assert(box.TopLeft.EqualsApprox(-box.BottomRight));

        if (_rotation == Angle.Zero) // TODO This shouldn't need to be here, but I just give up at this point I am going fucking insane looking at rotating blobs of pixels. I doubt anyone will ever even use rotated sprite views.?
        {
            _spriteSize = box.Size;
            return;
        }

        // Size does not auto-update with world rotation. So if it is not fixed by _worldRotation we will just take
        // the maximum possible size.
        var size = box.Size;
        var longestSide = MathF.Max(size.X, size.Y);
        var longestRotatedSide = Math.Max(longestSide, (size.X + size.Y) / MathF.Sqrt(2));
        _spriteSize = new Vector2(longestRotatedSide, longestRotatedSide);
    }

    protected override void Draw(IRenderHandle renderHandle)
    {
        if (!ResolveEntity(out var uid, out _) || _cachedSprite == null)
            return;

        var stretchVec = Stretch switch
        {
            StretchMode.Fit => Vector2.Min(Size / _spriteSize, Vector2.One),
            StretchMode.Fill => Size / _spriteSize,
            _ => Vector2.One,
        };
        var stretch = MathF.Min(stretchVec.X, stretchVec.Y);

        var offset = SpriteOffset
            ? Vector2.Zero
            : - (-_rotation).RotateVec(_cachedSprite.Offset * _scale) * new Vector2(1, -1) * EyeManager.PixelsPerMeter;

        var position = PixelSize / 2 + offset * stretch * UIScale;
        var scale = Scale * UIScale * stretch;

        var world = renderHandle.DrawingHandleWorld;
        var oldModulate = world.Modulate;
        world.Modulate *= Modulate * ActualModulateSelf;

        renderHandle.DrawEntity(
            uid,
            position,
            scale,
            _rotation,
            _rotation,
            OverrideDirection,
            _cachedSprite
        );

        world.Modulate = oldModulate;
    }

    private bool ResolveEntity(out EntityUid uid, [NotNullWhen(true)] out SpriteComponent? sprite)
    {
        sprite = null;
        uid = EntityUid.Invalid;

        if (!_entityMan.EntityExists(Entity.Owner) || _cachedSprite == null)
            return false;

        uid = Entity.Owner;
        sprite = _cachedSprite;

        return true;
    }
}

using System.Numerics;
using Content.Client._Sunrise.Sandbox.Access.Systems;
using Robust.Client.GameObjects;
using Content.Client.Stylesheets;
using Content.Shared.Access.Components;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Sandbox.Access.Overlays;

/// <summary>
/// Draws screen-space access labels for visible access readers while mapping helpers are enabled.
/// </summary>
public sealed partial class MappingAccessOverlay : Overlay
{
    /*
     * Main overlay rendering and shared state.
     */
    private const string OrLocKey = "mapping-access-overlay-or";
    private const float HorizontalMargin = 6f;
    private const float VerticalMargin = 4f;
    private const float ScreenPadding = 4f;
    private const float OutlinePadding = 1f;
    private const float ConnectorArrowLength = 10f;
    private const float ConnectorArrowHalfWidth = 4f;

    private static readonly Vector2 BackgroundPadding = new(4f, 3f);
    private static readonly Color TitleColor = Color.Aquamarine;
    private static readonly Color AccessColor = Color.Gold;
    private static readonly Color BackgroundColor = new Color(10, 12, 16).WithAlpha(0.72f);
    private static readonly Color OutlineColor = TitleColor.WithAlpha(0.85f);

    private readonly IEntityManager _ent;
    private readonly EntityLookupSystem _entityLookup;
    private readonly EntityQuery<PhysicsComponent> _physicsQuery;
    private readonly MappingAccessReaderResolver _readerResolver;
    private readonly SpriteSystem _spriteSystem;
    private readonly MappingAccessTightBounds _tightBounds;
    private readonly SharedTransformSystem _transformSystem;
    private readonly IPrototypeManager _prototypeManager;
    private readonly ILocalizationManager _loc;
    private readonly IUserInterfaceManager _uiManager;
    private readonly Font _font;
    private readonly Font _fontBold;

    private readonly List<string> _accessLines = new(8);
    private readonly List<UIBox2> _occupiedRects = new(16);

    /// <summary>
    /// Filters which reader body types receive labels.
    /// </summary>
    public MappingAccessBodyFilter BodyFilter { get; set; } = MappingAccessBodyFilter.Both;

    /// <summary>
    /// Restricts labels to access readers provided by contained electronics.
    /// </summary>
    public bool ElectronicsOnly { get; set; }

    /// <inheritdoc />
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    /// <summary>
    /// Creates a mapping overlay that renders access requirements for visible access readers.
    /// </summary>
    internal MappingAccessOverlay(
        IEntityManager entityManager,
        EntityLookupSystem entityLookup,
        SpriteSystem spriteSystem,
        IPrototypeManager prototypeManager,
        ILocalizationManager loc,
        IResourceCache resourceCache,
        IUserInterfaceManager uiManager,
        MappingAccessReaderResolver readerResolver,
        MappingAccessTightBounds tightBounds,
        EntityQuery<PhysicsComponent> physicsQuery)
    {
        _ent = entityManager;
        _entityLookup = entityLookup;
        _physicsQuery = physicsQuery;
        _readerResolver = readerResolver;
        _spriteSystem = spriteSystem;
        _tightBounds = tightBounds;
        _transformSystem = _ent.System<SharedTransformSystem>();
        _prototypeManager = prototypeManager;
        _loc = loc;
        _uiManager = uiManager;
        _font = resourceCache.NotoStack();
        _fontBold = resourceCache.NotoStack(variation: "Bold");
        ZIndex = 210;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var uiScale = _uiManager.RootControl.UIScale;
        var orText = _loc.GetString(OrLocKey);
        var lineHeight = MathF.Max(
            args.ScreenHandle.GetDimensions(_font, "Hg", uiScale).Y,
            args.ScreenHandle.GetDimensions(_fontBold, "Hg", uiScale).Y);
        var lineOffset = new Vector2(0f, lineHeight);
        var screenPadding = ScreenPadding * uiScale;
        var horizontalMargin = HorizontalMargin * uiScale;
        var verticalMargin = VerticalMargin * uiScale;
        _occupiedRects.Clear();

        var query = _ent.AllEntityQueryEnumerator<AccessReaderComponent, SpriteComponent, TransformComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var accessReader, out var sprite, out var transform, out var meta))
        {
            if (transform.MapID != args.MapId ||
                !accessReader.Enabled)
            {
                continue;
            }

            if (!TryGetDisplayedAccessReader(uid, accessReader, out var displayedReader) ||
                !displayedReader.Enabled ||
                displayedReader.AccessLists.Count == 0)
            {
                continue;
            }

            if (!_physicsQuery.TryComp(uid, out var physics) || !MatchesBodyFilter(physics.BodyType, BodyFilter))
                continue;

            var aabb = GetWorldBounds(uid, sprite, transform, in args);
            if (!aabb.Intersects(in args.WorldAABB))
                continue;

            var topLeft = args.ViewportControl.WorldToScreen(aabb.TopLeft);
            var topRight = args.ViewportControl.WorldToScreen(aabb.TopRight);
            var bottomLeft = args.ViewportControl.WorldToScreen(aabb.BottomLeft);
            var bottomRight = args.ViewportControl.WorldToScreen(aabb.BottomRight);

            if (!IntersectsViewport(args.Viewport.Size, topLeft, topRight, bottomLeft, bottomRight))
                continue;

            BuildAccessLines(displayedReader, orText);
            if (_accessLines.Count == 0)
                continue;

            if (meta.EntityPrototype?.ID is not { } protoId)
                continue;

            var title = protoId;

            var blockWidth = args.ScreenHandle.GetDimensions(_fontBold, title, uiScale).X;
            foreach (var line in _accessLines)
            {
                var lineWidth = args.ScreenHandle.GetDimensions(_font, line, uiScale).X;
                if (lineWidth > blockWidth)
                    blockWidth = lineWidth;
            }

            var blockHeight = lineHeight * (_accessLines.Count + 1);

            var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
            var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
            var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
            var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));
            var outlinePadding = OutlinePadding * uiScale;
            var outlineRect = UIBox2.FromDimensions(
                new Vector2(minX - outlinePadding, minY - outlinePadding),
                new Vector2(maxX - minX, maxY - minY) + Vector2.One * (outlinePadding * 2f));

            var scaledBackgroundPadding = BackgroundPadding * uiScale;
            var contentSize = new Vector2(blockWidth, blockHeight);
            var backgroundSize = contentSize + scaledBackgroundPadding * 2f;
            if (!TryGetBackgroundRect(
                    outlineRect,
                    backgroundSize,
                    args.Viewport.Size,
                    screenPadding,
                    horizontalMargin,
                    verticalMargin,
                    out var backgroundRect,
                    out var placement))
                continue;

            args.ScreenHandle.DrawRect(backgroundRect, BackgroundColor);

            var arrowLength = ConnectorArrowLength * uiScale;
            var arrowHalfWidth = ConnectorArrowHalfWidth * uiScale;
            Vector2 connectorStart;
            Vector2 connectorEnd;
            Vector2 arrowLeft;
            Vector2 arrowRight;

            switch (placement)
            {
                case LabelPlacement.Above:
                    connectorStart = new Vector2(backgroundRect.Center.X, backgroundRect.Bottom);
                    connectorEnd = new Vector2(outlineRect.Center.X, outlineRect.Top);
                    arrowLeft = connectorEnd + new Vector2(-arrowHalfWidth, arrowLength);
                    arrowRight = connectorEnd + new Vector2(arrowHalfWidth, arrowLength);
                    break;
                case LabelPlacement.Right:
                    connectorStart = new Vector2(backgroundRect.Left, backgroundRect.Center.Y);
                    connectorEnd = new Vector2(outlineRect.Right, outlineRect.Center.Y);
                    arrowLeft = connectorEnd + new Vector2(-arrowLength, -arrowHalfWidth);
                    arrowRight = connectorEnd + new Vector2(-arrowLength, arrowHalfWidth);
                    break;
                case LabelPlacement.Left:
                    connectorStart = new Vector2(backgroundRect.Right, backgroundRect.Center.Y);
                    connectorEnd = new Vector2(outlineRect.Left, outlineRect.Center.Y);
                    arrowLeft = connectorEnd + new Vector2(arrowLength, -arrowHalfWidth);
                    arrowRight = connectorEnd + new Vector2(arrowLength, arrowHalfWidth);
                    break;
                default:
                    connectorStart = new Vector2(backgroundRect.Center.X, backgroundRect.Top);
                    connectorEnd = new Vector2(outlineRect.Center.X, outlineRect.Bottom);
                    arrowLeft = connectorEnd + new Vector2(-arrowHalfWidth, -arrowLength);
                    arrowRight = connectorEnd + new Vector2(arrowHalfWidth, -arrowLength);
                    break;
            }

            args.ScreenHandle.DrawLine(connectorStart, connectorEnd, OutlineColor);
            args.ScreenHandle.DrawLine(connectorEnd, arrowLeft, OutlineColor);
            args.ScreenHandle.DrawLine(connectorEnd, arrowRight, OutlineColor);
            _occupiedRects.Add(backgroundRect);

            var startPos = backgroundRect.TopLeft + scaledBackgroundPadding;

            var currentPos = startPos;
            args.ScreenHandle.DrawString(_fontBold, currentPos, title, uiScale, TitleColor);
            currentPos += lineOffset;

            foreach (var line in _accessLines)
            {
                args.ScreenHandle.DrawString(_font, currentPos, line, uiScale, AccessColor);
                currentPos += lineOffset;
            }
        }
    }

    private enum LabelPlacement : byte
    {
        Below,
        Above,
        Right,
        Left,
    }
}

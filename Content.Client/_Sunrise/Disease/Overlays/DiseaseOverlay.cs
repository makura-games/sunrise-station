using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;

namespace Content.Client._Sunrise.Disease.Overlays;

/// <summary>
/// Подписывает под очагами их болезни. Сам очаг подсвечивает ореол на спрайте,
/// поэтому оверлей включается только для админских меток с подписями.
/// </summary>
public sealed partial class DiseaseOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    /// <summary>
    /// Отступ подписи от нижнего края сущности, в пикселях до масштаба интерфейса.
    /// </summary>
    private const float Padding = 2f;

    /// <summary>
    /// Высота строки подписи, в пикселях до масштаба интерфейса.
    /// </summary>
    private const float LineHeight = 14f;

    private readonly DiseaseOverlaySystem _system;
    private readonly EntityLookupSystem _lookup;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public DiseaseOverlay(DiseaseOverlaySystem system)
    {
        IoCManager.InjectDependencies(this);

        _system = system;
        _lookup = _entMan.System<EntityLookupSystem>();
        _xformQuery = _entMan.GetEntityQuery<TransformComponent>();
        _font = _resourceCache.NotoStack();

        // Поверх мира, но под админскими подписями игроков.
        ZIndex = 190;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var uiScale = _ui.RootControl.UIScale;
        var padding = Padding * uiScale;
        var lineOffset = new Vector2(0f, LineHeight * uiScale);

        foreach (var marker in _system.Markers)
        {
            if (marker.Lines.Length == 0)
                continue;

            // Метка может прийти раньше, чем сущность попала в PVS, или пережить её удаление.
            if (!_entMan.TryGetEntity(marker.Entity, out var uid) ||
                !_xformQuery.TryComp(uid, out var xform) ||
                xform.MapID != args.MapId)
                continue;

            var aabb = _lookup.GetWorldAABB(uid.Value, xform);
            if (!aabb.Intersects(args.WorldAABB))
                continue;

            var box = ToScreenBox(aabb, padding);
            var color = DiseaseOverlaySystem.GetColor(marker.Kind).WithAlpha(1f);

            var textPosition = new Vector2(box.Left, box.Bottom + padding);
            foreach (var line in marker.Lines)
            {
                handle.DrawString(_font, textPosition, line, uiScale, color);
                textPosition += lineOffset;
            }
        }
    }

    /// <summary>
    /// Переводит мировые границы сущности в экранные с отступом. Камера может быть повёрнута, поэтому берутся все четыре угла.
    /// </summary>
    private UIBox2 ToScreenBox(Box2 aabb, float padding)
    {
        var a = _eye.WorldToScreen(aabb.BottomLeft);
        var b = _eye.WorldToScreen(aabb.BottomRight);
        var c = _eye.WorldToScreen(aabb.TopLeft);
        var d = _eye.WorldToScreen(aabb.TopRight);

        // На экране ось Y смотрит вниз, поэтому верх рамки — минимальный Y.
        return new UIBox2(
            MathF.Min(MathF.Min(a.X, b.X), MathF.Min(c.X, d.X)) - padding,
            MathF.Min(MathF.Min(a.Y, b.Y), MathF.Min(c.Y, d.Y)) - padding,
            MathF.Max(MathF.Max(a.X, b.X), MathF.Max(c.X, d.X)) + padding,
            MathF.Max(MathF.Max(a.Y, b.Y), MathF.Max(c.Y, d.Y)) + padding);
    }
}

using Content.Client.Graphics;
using Content.Client.Viewport;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Overlays;
using Content.Shared.Inventory.Events;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Disease.Overlays;

/// <summary>
/// Принимает метки оверлея болезней от сервера и подсвечивает очаги ореолом по контуру спрайта.
/// </summary>
/// <remarks>
/// Сам клиент заражённых не вычисляет: всё, что он подсвечивает, сервер прислал именно этому игроку.
/// Ореол живёт пост-шейдером на спрайте, поэтому повторяет форму предмета и не спорит с обводкой
/// при наведении. Админские подписи болезней рисует отдельный экранный оверлей.
/// </remarks>
public sealed partial class DiseaseOverlaySystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    /// <summary>
    /// Идентификатор пост-шейдера ореола на спрайте.
    /// </summary>
    private const string GlowShaderId = "disease-vision-glow";

    /// <summary>
    /// Ширина ореола в пикселях спрайта до масштаба рендера.
    /// </summary>
    private const float GlowWidth = 3f;

    private static readonly ProtoId<ShaderPrototype> GlowShader = "SunriseDiseaseVisionGlow";

    private static readonly Color SourceColor = Color.FromHex("#9BE05ACC");
    private static readonly Color CloudColor = Color.FromHex("#E0C94FCC");
    private static readonly Color CarrierColor = Color.FromHex("#E0524ACC");

    private readonly List<DiseaseOverlayMarker> _markers = [];

    /// <summary>
    /// Сущности, на которых сейчас висит ореол, и вид очага, под который подобран шейдер.
    /// </summary>
    private readonly Dictionary<EntityUid, DiseaseOverlayKind> _glowing = new();

    private readonly HashSet<EntityUid> _present = new();
    private readonly List<EntityUid> _stale = new();
    private readonly Dictionary<(DiseaseOverlayKind Kind, int RenderScale), ShaderInstance> _shaders = new();

    private DiseaseOverlay _overlay = default!;
    private int _renderScale = 1;

    /// <summary>
    /// Пришли ли последние метки с подписями, то есть от админского оверлея, а не от очков.
    /// </summary>
    private bool _hasLines;

    /// <summary>
    /// Метки, которые сейчас подсвечены.
    /// </summary>
    public IReadOnlyList<DiseaseOverlayMarker> Markers => _markers;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new DiseaseOverlay(this);

        SubscribeNetworkEvent<DiseaseOverlayUpdateEvent>(OnOverlayUpdate);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _markers.Clear();
        ClearGlows();
        _overlayManager.RemoveOverlay(_overlay);

        foreach (var shader in _shaders.Values)
        {
            shader.Dispose();
        }

        _shaders.Clear();
    }

    private void OnOverlayUpdate(DiseaseOverlayUpdateEvent ev)
    {
        _markers.Clear();
        _hasLines = false;
        foreach (var entry in ev.Entries)
        {
            var lines = BuildLines(entry);
            _hasLines |= lines.Length > 0;
            _markers.Add(new DiseaseOverlayMarker(entry.Entity, entry.Kind, lines));
        }

        // Экранный оверлей нужен только под подписи: ореол рисуют сами спрайты.
        if (!_hasLines)
            _overlayManager.RemoveOverlay(_overlay);
        else if (!_overlayManager.HasOverlay<DiseaseOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }

    /// <summary>
    /// Снятые очки гасят подсветку сразу, не дожидаясь пустого списка от сервера.
    /// </summary>
    /// <remarks>
    /// Админские метки от очков не зависят, их выключает только команда.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnVisionUnequipped(Entity<DiseaseVisionComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.EquipTarget != _player.LocalEntity || (args.SlotFlags & ent.Comp.Slots) == 0 || _hasLines)
            return;

        _markers.Clear();
    }

    /// <summary>
    /// Каждый кадр догоняет ореолы за метками: сущность может войти в PVS позже метки,
    /// а масштаб рендера — смениться вместе с окном.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_markers.Count == 0 && _glowing.Count == 0)
            return;

        var renderScale = _eye.MainViewport.GetRenderScale();
        var rescaled = renderScale != _renderScale;
        _renderScale = renderScale;

        _present.Clear();
        foreach (var marker in _markers)
        {
            if (!TryGetEntity(marker.Entity, out var uid) || !TryComp(uid, out SpriteComponent? sprite))
                continue;

            _present.Add(uid.Value);
            if (!rescaled && _glowing.TryGetValue(uid.Value, out var kind) && kind == marker.Kind)
                continue;

            SetGlow((uid.Value, sprite), marker.Kind);
        }

        _stale.Clear();
        foreach (var uid in _glowing.Keys)
        {
            if (!_present.Contains(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            RemoveGlow(uid);
        }
    }

    private void SetGlow(Entity<SpriteComponent> ent, DiseaseOverlayKind kind)
    {
        _sprite.SetPostShader(ent.AsNullable(), new SpriteComponent.PostShaderArgs(GlowShaderId, GetShader(kind, _renderScale))
        {
            // Обводка при наведении ложится поверх ореола, а не обводит его.
            After = ContentPostShaderIds.AfterBaseEffects,
            Before = ContentPostShaderIds.BeforeOutlines,
        });

        _glowing[ent.Owner] = kind;
    }

    private void RemoveGlow(EntityUid uid)
    {
        _glowing.Remove(uid);

        if (TryComp(uid, out SpriteComponent? sprite))
            _sprite.RemovePostShader((uid, sprite), GlowShaderId);
    }

    private void ClearGlows()
    {
        _stale.Clear();
        _stale.AddRange(_glowing.Keys);
        foreach (var uid in _stale)
        {
            RemoveGlow(uid);
        }
    }

    private ShaderInstance GetShader(DiseaseOverlayKind kind, int renderScale)
    {
        if (_shaders.TryGetValue((kind, renderScale), out var shader))
            return shader;

        shader = _prototype.Index(GlowShader).InstanceUnique();
        shader.SetParameter("glow_color", GetColor(kind));
        shader.SetParameter("glow_width", GlowWidth * renderScale);
        _shaders[(kind, renderScale)] = shader;
        return shader;
    }

    /// <summary>
    /// Подписи под меткой. Строки собираются при получении, чтобы не локализовать их каждый кадр.
    /// </summary>
    private string[] BuildLines(DiseaseOverlayEntry entry)
    {
        var lines = new string[entry.Diseases.Count];
        for (var i = 0; i < entry.Diseases.Count; i++)
        {
            var disease = entry.Diseases[i];
            var name = Loc.GetString(disease.Name);

            lines[i] = disease.Stage > 0
                ? Loc.GetString("disease-overlay-carrier-line",
                    ("name", name),
                    ("strain", disease.StrainCode),
                    ("stage", disease.Stage),
                    ("count", disease.StageCount))
                : Loc.GetString("disease-overlay-source-line",
                    ("name", name),
                    ("strain", disease.StrainCode));
        }

        return lines;
    }

    /// <summary>
    /// Цвет очага: заражённый предмет, облако или носитель.
    /// </summary>
    public static Color GetColor(DiseaseOverlayKind kind)
    {
        return kind switch
        {
            DiseaseOverlayKind.Carrier => CarrierColor,
            DiseaseOverlayKind.Cloud => CloudColor,
            _ => SourceColor,
        };
    }
}

/// <summary>
/// Готовая к отрисовке метка.
/// </summary>
/// <param name="Entity">Подсвеченная сущность.</param>
/// <param name="Kind">Вид очага, от него зависит цвет.</param>
/// <param name="Lines">Подписи под меткой. Пусто у очков вирусолога.</param>
public readonly record struct DiseaseOverlayMarker(NetEntity Entity, DiseaseOverlayKind Kind, string[] Lines);

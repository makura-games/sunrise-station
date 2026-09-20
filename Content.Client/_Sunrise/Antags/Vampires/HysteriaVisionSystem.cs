using Content.Shared._Sunrise.Antags.Vampires.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Antags.Vampires;

/// <summary>
/// Клиентская система управления HysteriaVisionOverlay.
/// Добавляет/убирает оверлей в зависимости от наличия HysteriaVisionComponent у локального игрока.
/// </summary>
public sealed partial class HysteriaVisionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private EntityQuery<HysteriaVisionComponent> _hysteriaQuery;
    private HysteriaVisionOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _hysteriaQuery = GetEntityQuery<HysteriaVisionComponent>();

        SubscribeLocalEvent<HysteriaVisionComponent, ComponentStartup>(OnHysteriaStartup);
        SubscribeLocalEvent<HysteriaVisionComponent, ComponentShutdown>(OnHysteriaShutdown);
        SubscribeLocalEvent<HysteriaVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<HysteriaVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    private void OnHysteriaStartup(Entity<HysteriaVisionComponent> ent, ref ComponentStartup args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
            AddOverlay();
    }

    private void OnHysteriaShutdown(Entity<HysteriaVisionComponent> ent, ref ComponentShutdown args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
            RemoveOverlay();
    }

    private void OnPlayerAttached(Entity<HysteriaVisionComponent> ent, ref LocalPlayerAttachedEvent args)
        => AddOverlay();

    private void OnPlayerDetached(Entity<HysteriaVisionComponent> ent, ref LocalPlayerDetachedEvent args)
        => RemoveOverlay();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Проверка необходимости убрать оверлей из-за истечения
        var player = _playerManager.LocalEntity;
        if (player == null || !_hysteriaQuery.TryComp(player.Value, out var hysteria))
            return;

        // Сервер владеет жизненным циклом компонента; клиент лишь скрывает оверлей, ожидая репликации.
        if (_timing.CurTime > hysteria.EndTime)
            RemoveOverlay();
    }

    private void AddOverlay()
    {
        if (_overlay != null)
            return;

        _overlay = new HysteriaVisionOverlay();
        _overlayManager.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        _overlay = null;
    }
}

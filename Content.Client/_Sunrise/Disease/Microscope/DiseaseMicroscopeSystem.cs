using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Microscope;
using Content.Shared._Sunrise.Disease.Systems;

namespace Content.Client._Sunrise.Disease.Microscope;

/// <summary>
/// Клиентская часть микроскопа: перерисовывает окно после предсказанного хода
/// и после каждого реплицированного состояния машины.
/// </summary>
/// <remarks>
/// За один кадр поле может смениться несколько раз: пришло серверное состояние без хода,
/// затем ход заново применился при повторном предсказании. Если перерисовывать окно на каждое
/// изменение, анимация колоний перезапускается и шарики дёргаются. Поэтому перерисовка
/// откладывается до конца кадра и получает уже итоговое поле.
/// </remarks>
public sealed partial class DiseaseMicroscopeSystem : SharedDiseaseMicroscopeSystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    /// <summary>
    /// Машины, чьё окно нужно перерисовать в конце кадра.
    /// </summary>
    private readonly HashSet<EntityUid> _dirtyUis = [];

    public override void Shutdown()
    {
        base.Shutdown();
        _dirtyUis.Clear();
    }

    [SubscribeLocalEvent]
    private void OnState(Entity<DiseaseMicroscopeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateUi(ent);
    }

    protected override void UpdateUi(Entity<DiseaseMicroscopeComponent> ent)
    {
        _dirtyUis.Add(ent.Owner);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_dirtyUis.Count == 0)
            return;

        foreach (var uid in _dirtyUis)
        {
            if (!TerminatingOrDeleted(uid) && _ui.TryGetOpenUi(uid, DiseaseMicroscopeUiKey.Key, out var bui))
                bui.Update();
        }

        _dirtyUis.Clear();
    }
}

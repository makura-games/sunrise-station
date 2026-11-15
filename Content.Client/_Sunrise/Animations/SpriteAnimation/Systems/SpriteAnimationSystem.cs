using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Animations.SpriteAnimation.Systems;

/// <summary>
/// Собирает несколько анимаций спрайта поверх одной базы: смещения и углы складываются, масштаб перемножается.
/// </summary>
/// <remarks>
/// Каждый key живёт отдельно, поэтому замена одной анимации не сбивает остальные.
/// Прямая запись в SpriteComponent считается новой базой. Если меняется именно база, лучше использовать SetBase методы:
/// Относительное изменение уже анимированного значения невозможно нормально отделить от самой анимации.
/// На запаузенных сущностях треки и циклы не обновляются.
/// </remarks>
public sealed partial class SpriteAnimationSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private EntityQuery<SpriteAnimationComponent> _animationQuery = default!;

    /// <summary>
    /// Сущности, состояние которых можно удалить после обхода
    /// </summary>
    private readonly List<EntityUid> _done = new();

    /// <summary>
    /// Снимок циклов для безопасного вызова фабрик после обхода состояний
    /// </summary>
    private readonly List<(EntityUid Uid, string Key, SpriteAnimationLoop Loop)> _loops = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(AnimationPlayerSystem));
        base.Initialize();
        SubscribeLocalEvent<SpriteComponent, ComponentShutdown>(OnSpriteShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _done.Clear();
        _loops.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _done.Clear();
        _loops.Clear();
        var query = EntityQueryEnumerator<SpriteAnimationComponent, SpriteComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var state, out var sprite, out var meta))
        {
            if (IsPaused(uid, meta))
                continue;

            ApplyContributions((uid, sprite), state, frameTime);

            foreach (var (key, loop) in state.Loops)
                _loops.Add((uid, key, loop));

            if (state is { HasOffset: false, HasScale: false, HasRotation: false, Loops.Count: 0 })
                _done.Add(uid);
        }

        foreach (var uid in _done)
            RemComp<SpriteAnimationComponent>(uid);

        foreach (var (uid, key, loop) in _loops)
        {
            if (!_animationQuery.TryComp(uid, out var state) ||
                !state.Loops.TryGetValue(key, out var current) || current != loop)
                continue;
            if (loop.CanPlay?.Invoke(uid) == false)
            {
                Stop(uid, key);
                continue;
            }
            if (state.Offsets.TryGetValue(key, out var offset) && !offset.Done ||
                state.Scales.TryGetValue(key, out var scale) && !scale.Done ||
                state.Rotations.TryGetValue(key, out var rotation) && !rotation.Done)
                continue;

            loop.Play(uid);
            if (_animationQuery.TryComp(uid, out state) && TryComp<SpriteComponent>(uid, out var sprite))
            {
                ApplyContributions((uid, sprite), state, 0f);
            }
        }
    }

    private void OnSpriteShutdown(Entity<SpriteComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            RemComp<SpriteAnimationComponent>(ent);
    }
}

using System.Numerics;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.GameStates;
using Robust.Shared.Animations;
using Robust.Shared.Maths;

namespace Content.Client._Sunrise.Animations;

/// <summary>
/// Собирает несколько анимаций спрайта поверх одной базы: смещения и углы складываются, масштаб перемножается.
/// </summary>
/// <remarks>
/// Каждый key живёт отдельно, поэтому замена одной анимации не сбивает остальные.
/// Прямая запись в SpriteComponent считается новой базой. Если меняется именно база, лучше использовать SetBase методы:
/// относительное изменение уже анимированного значения невозможно нормально отделить от самой анимации.
/// При выходе из PVS текущие треки сбрасываются до базы, а зарегистрированные циклы остаются и запускаются заново после возврата.
/// На запаузенных сущностях треки и циклы не обновляются.
/// </remarks>
public sealed partial class SpriteAnimationSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IClientGameStateManager _gameState = default!;

    private readonly Dictionary<EntityUid, SpriteAnimationState> _states = new();
    private readonly List<EntityUid> _done = new();
    private readonly List<(EntityUid Uid, string Key, SpriteAnimationTrack Track)> _completed = new();
    private readonly List<(EntityUid Uid, string Key, SpriteAnimationLoop Loop)> _loops = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(AnimationPlayerSystem));
        base.Initialize();
        SubscribeLocalEvent<SpriteComponent, ComponentShutdown>(OnSpriteShutdown);
        _gameState.GameStateApplied += OnStateApplied;
    }

    public override void Shutdown()
    {
        _gameState.GameStateApplied -= OnStateApplied;
        base.Shutdown();
        _states.Clear();
        _done.Clear();
        _completed.Clear();
        _loops.Clear();
    }

    private void OnStateApplied(GameStateAppliedArgs args)
    {
        foreach (var netEntity in args.Detached)
        {
            if (!TryGetEntity(netEntity, out var uid) || !TryComp<MetaDataComponent>(uid, out var meta) ||
                (meta.Flags & MetaDataFlags.Detached) == 0)
                continue;

            if (_states.TryGetValue(uid.Value, out var state) && TryComp<SpriteComponent>(uid, out var sprite))
            {
                state.Offsets.Clear();
                state.Scales.Clear();
                state.Rotations.Clear();
                state.Notifications.Clear();
                UpdateOffset((uid.Value, sprite), state, 0f);
                UpdateScale((uid.Value, sprite), state, 0f);
                UpdateRotation((uid.Value, sprite), state, 0f);
                if (state.Loops.Count == 0)
                    _states.Remove(uid.Value);
            }

            var ev = new SpriteAnimationResetEvent();
            RaiseLocalEvent(uid.Value, ref ev);
        }
    }

    private void OnSpriteShutdown(Entity<SpriteComponent> ent, ref ComponentShutdown args)
    {
        _states.Remove(ent.Owner);
    }

    /// <summary>
    /// Возвращает базовый offset без анимаций. Внешняя запись в спрайт принимается за новую базу.
    /// </summary>
    public Vector2 GetBaseOffset(Entity<SpriteComponent> ent)
    {
        if (!_states.TryGetValue(ent.Owner, out var state) || !state.HasOffset)
            return ent.Comp.Offset;

        if (ent.Comp.Offset != state.LastOffset)
            state.Offset = ent.Comp.Offset;

        return state.Offset;
    }

    /// <summary>
    /// Меняет базовый offset и сразу накладывает поверх него активные анимации.
    /// </summary>
    public void SetBaseOffset(Entity<SpriteComponent> ent, Vector2 offset)
    {
        if (_states.TryGetValue(ent, out var state) && state.HasOffset)
        {
            state.Offset = offset;
            offset += state.OffsetContribution;
            state.LastOffset = offset;
        }

        _sprite.SetOffset(ent.AsNullable(), offset);
    }

    /// <summary>
    /// Меняет базовый scale и сразу накладывает поверх него активные множители.
    /// </summary>
    public void SetBaseScale(Entity<SpriteComponent> ent, Vector2 scale)
    {
        if (_states.TryGetValue(ent, out var state) && state.HasScale)
        {
            state.Scale = scale;
            scale *= state.ScaleContribution;
            state.LastScale = scale;
        }

        _sprite.SetScale(ent.AsNullable(), scale);
    }

    /// <summary>
    /// Возвращает базовый rotation без анимаций. Внешняя запись в спрайт принимается за новую базу.
    /// </summary>
    public Angle GetBaseRotation(Entity<SpriteComponent> ent)
    {
        if (!_states.TryGetValue(ent, out var state) || !state.HasRotation)
            return ent.Comp.Rotation;

        if (!ent.Comp.Rotation.Equals(state.LastRotation))
            state.Rotation = ent.Comp.Rotation;

        return state.Rotation;
    }

    /// <summary>
    /// Возвращает последний rotation-вклад указанного key, не двигая анимацию дальше.
    /// </summary>
    public Angle GetRotationOffset(EntityUid uid, string key)
    {
        if (!_states.TryGetValue(uid, out var state) || !state.Rotations.TryGetValue(key, out var track))
            return Angle.Zero;

        return new Angle(track.Value.X);
    }

    /// <summary>
    /// Меняет базовый rotation и сразу накладывает поверх него активные анимации.
    /// </summary>
    public void SetBaseRotation(Entity<SpriteComponent> ent, Angle rotation)
    {
        if (_states.TryGetValue(ent, out var state) && state.HasRotation)
        {
            state.Rotation = rotation;
            rotation += state.RotationContribution;
            state.LastRotation = rotation;
        }

        _sprite.SetRotation(ent.AsNullable(), rotation);
    }

    /// <summary>
    /// Проверяет, есть ли у key активный трек хотя бы в одном канале. Завершённый Hold тоже считается активным.
    /// Одна регистрация цикла без треков за проигрывание не считается.
    /// </summary>
    public bool IsPlaying(EntityUid uid, string key)
    {
        return _states.TryGetValue(uid, out var state) &&
               (state.Offsets.ContainsKey(key) || state.Scales.ContainsKey(key) || state.Rotations.ContainsKey(key));
    }

    /// <summary>
    /// Проверяет регистрацию цикла, даже если сущность сейчас на паузе или вне PVS.
    /// </summary>
    public bool IsLooping(EntityUid uid, string key)
        => _states.TryGetValue(uid, out var state) && state.Loops.ContainsKey(key);

    /// <summary>
    /// Регистрирует цикл, который пересоздаёт закончившиеся треки и переживает выход из PVS.
    /// </summary>
    /// <remarks>
    /// play должен создавать треки под переданным key. Новый круг начинается только после завершения старых треков.
    /// Если сущность видима и не на паузе, первый запуск происходит сразу.
    /// canPlay проверяется каждый видимый кадр: false останавливает key и удаляет цикл.
    /// Stop тоже удаляет регистрацию, обычные Play-методы заменяют только треки.
    /// Сущности без SpriteComponent игнорируются.
    /// </remarks>
    public void PlayLoop(EntityUid uid, string key, Action<EntityUid> play, Func<EntityUid, bool>? canPlay = null)
    {
        if (!HasComp<SpriteComponent>(uid))
            return;
        if (!_states.TryGetValue(uid, out var state))
        {
            state = new SpriteAnimationState();
            _states.Add(uid, state);
        }
        state.Loops[key] = new SpriteAnimationLoop(play, canPlay);
        var meta = MetaData(uid);
        if (meta.EntityPaused || (meta.Flags & MetaDataFlags.Detached) != 0)
            return;
        if (canPlay?.Invoke(uid) == false)
            Stop(uid, key);
        else if (!IsPlaying(uid, key))
            play(uid);
    }

    /// <summary>
    /// Запускает добавочную rotation-анимацию с Release в конце.
    /// </summary>
    public void PlayRotation(EntityUid uid, string key, params (Angle Value, float Time)[] frames)
        => PlayRotation(uid, key, SpriteAnimationEndMode.Release, frames);

    /// <summary>
    /// Заменяет rotation-вклад этого key линейной интерполяцией переданных углов.
    /// </summary>
    /// <remarks>
    /// Time у кадра — время от предыдущего кадра, Time первого кадра игнорируется.
    /// Углы интерполируются как переданы, без автоматического выбора короткой дуги.
    /// Release убирает вклад в конце, Hold оставляет его до замены или Stop.
    /// В спрайт значение попадёт на общей сборке кадра, completion-событие этот overload не кидает.
    /// Меньше двух кадров и сущность без SpriteComponent игнорируются.
    /// </remarks>
    public void PlayRotation(EntityUid uid, string key, SpriteAnimationEndMode endMode, params (Angle Value, float Time)[] frames)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || frames.Length < 2)
            return;

        if (!_states.TryGetValue(uid, out var state))
        {
            state = new SpriteAnimationState();
            _states.Add(uid, state);
        }

        if (!state.HasRotation)
        {
            state.Rotation = sprite.Rotation;
            state.LastRotation = sprite.Rotation;
            state.HasRotation = true;
        }

        var values = new (Vector2 Value, float Time)[frames.Length];
        for (var i = 0; i < frames.Length; i++)
            values[i] = (new Vector2((float) frames[i].Value.Theta, 0f), frames[i].Time);

        state.Notifications.Remove(key);
        state.Rotations[key] = new SpriteAnimationTrack(values, false, endMode);
    }

    /// <summary>
    /// Переводит один linear/cubic трек SpriteComponent.Offset в добавочный offset этой системы.
    /// </summary>
    /// <remarks>
    /// origin вычитается из значений кадров, а не из текущего offset спрайта.
    /// Первый кадр берётся из прошлого вклада этого key, либо из нуля.
    /// Нужно минимум два Vector2 keyframe без easing и с неотрицательным временем.
    /// Length может быть длиннее кадров, но не короче их общей длины.
    /// При обычном завершении, включая Hold, кидается SpriteAnimationCompletedEvent. Stop и PVS-reset событие не кидают.
    /// Пустая анимация и сущность без SpriteComponent просто игнорируются.
    /// </remarks>
    /// <exception cref="NotSupportedException">Анимация не подходит под поддерживаемый формат трека.</exception>
    public void PlayOffset(EntityUid uid, Animation animation, string key, Vector2 origin,
        SpriteAnimationEndMode endMode = SpriteAnimationEndMode.Release)
    {
        if (animation.AnimationTracks.Count == 0 || !HasComp<SpriteComponent>(uid))
            return;

        var frames = ReadFrames(animation, nameof(SpriteComponent.Offset), origin, out var cubic);
        var from = _states.TryGetValue(uid, out var state) && state.Offsets.TryGetValue(key, out var previous)
            ? previous.Value : Vector2.Zero;
        frames[0] = (from, 0f);
        PlayOffset(uid, key, cubic, endMode, frames);
        _states[uid].Notifications[key] = _states[uid].Offsets[key];
    }

    /// <summary>
    /// Запускает добавочную offset-анимацию с Release в конце.
    /// </summary>
    public void PlayOffset(EntityUid uid, string key, bool cubic, params (Vector2 Value, float Time)[] frames)
        => PlayOffset(uid, key, cubic, SpriteAnimationEndMode.Release, frames);

    /// <summary>
    /// Заменяет offset-вклад этого key. Интерполяция cubic либо linear, в зависимости от параметра.
    /// </summary>
    /// <remarks>
    /// Значения — это вклад поверх базы, а не абсолютный offset. Time считается от предыдущего кадра.
    /// Time первого кадра игнорируется. Release убирает вклад в конце, Hold оставляет его.
    /// В спрайт значение попадёт на общей сборке кадра, completion-событие этот overload не кидает.
    /// Меньше двух кадров и сущность без SpriteComponent игнорируются.
    /// </remarks>
    public void PlayOffset(EntityUid uid, string key, bool cubic, SpriteAnimationEndMode endMode, params (Vector2 Value, float Time)[] frames)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || frames.Length < 2)
            return;

        if (!_states.TryGetValue(uid, out var state))
        {
            state = new SpriteAnimationState();
            _states.Add(uid, state);
        }

        if (!state.HasOffset)
        {
            state.Offset = sprite.Offset;
            state.LastOffset = sprite.Offset;
            state.HasOffset = true;
        }

        state.Notifications.Remove(key);
        state.Offsets[key] = new SpriteAnimationTrack(frames, cubic, endMode);
    }

    /// <summary>
    /// Переводит linear-трек SpriteComponent.Scale в множители масштаба с Release в конце.
    /// </summary>
    /// <remarks>
    /// Значения считаются множителями относительно базы, а не абсолютным scale спрайта.
    /// Правила кадров, Length и completion-события такие же, как у offset-адаптера, только без origin.
    /// Пустая анимация и сущность без SpriteComponent игнорируются.
    /// </remarks>
    /// <exception cref="NotSupportedException">Анимация не подходит под поддерживаемый формат трека.</exception>
    public void PlayScale(EntityUid uid, Animation animation, string key)
    {
        if (animation.AnimationTracks.Count == 0 || !HasComp<SpriteComponent>(uid))
            return;

        var frames = ReadFrames(animation, nameof(SpriteComponent.Scale), Vector2.Zero, out var cubic);
        if (cubic)
            throw new NotSupportedException("scale animations must use linear interpolation");
        PlayScale(uid, key, frames);
        _states[uid].Notifications[key] = _states[uid].Scales[key];
    }

    /// <summary>
    /// Запускает анимацию множителя scale с Release в конце.
    /// </summary>
    public void PlayScale(EntityUid uid, string key, params (Vector2 Value, float Time)[] frames)
        => PlayScale(uid, key, SpriteAnimationEndMode.Release, frames);

    /// <summary>
    /// Заменяет scale-вклад этого key с линейной интерполяцией множителей.
    /// </summary>
    /// <remarks>
    /// Vector2.One не меняет базовый scale. Time считается от предыдущего кадра, Time первого игнорируется.
    /// Release убирает множитель в конце, Hold оставляет его до замены или Stop.
    /// В спрайт значение попадёт на общей сборке кадра, completion-событие этот overload не кидает.
    /// Меньше двух кадров и сущность без SpriteComponent игнорируются.
    /// </remarks>
    public void PlayScale(EntityUid uid, string key, SpriteAnimationEndMode endMode, params (Vector2 Value, float Time)[] frames)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || frames.Length < 2)
            return;

        if (!_states.TryGetValue(uid, out var state))
        {
            state = new SpriteAnimationState();
            _states.Add(uid, state);
        }

        if (!state.HasScale)
        {
            state.Scale = sprite.Scale;
            state.LastScale = sprite.Scale;
            state.HasScale = true;
        }

        state.Notifications.Remove(key);
        state.Scales[key] = new SpriteAnimationTrack(frames, false, endMode);
    }

    /// <summary>
    /// Перематывает все треки этого key на указанное время от начала. Отрицательное время считается нулём.
    /// </summary>
    /// <remarks>
    /// Можно мотать назад. Отсутствующие треки игнорируются, спрайт обновится при следующей сборке кадра.
    /// </remarks>
    public void Seek(EntityUid uid, string key, float elapsed)
    {
        if (!_states.TryGetValue(uid, out var state))
            return;

        if (state.Offsets.TryGetValue(key, out var offset))
            offset.Seek(elapsed);
        if (state.Scales.TryGetValue(key, out var scale))
            scale.Seek(elapsed);
        if (state.Rotations.TryGetValue(key, out var rotation))
            rotation.Seek(elapsed);
    }

    /// <summary>
    /// Полностью убирает key: его треки, цикл и ожидающее completion-событие.
    /// Спрайт сразу пересобирается без этого вклада, completion-событие не кидается.
    /// </summary>
    public void Stop(EntityUid uid, string key)
    {
        if (!_states.TryGetValue(uid, out var state))
            return;

        state.Offsets.Remove(key);
        state.Scales.Remove(key);
        state.Rotations.Remove(key);
        state.Notifications.Remove(key);
        state.Loops.Remove(key);

        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            UpdateOffset((uid, sprite), state, 0f);
            UpdateScale((uid, sprite), state, 0f);
            UpdateRotation((uid, sprite), state, 0f);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _done.Clear();
        _completed.Clear();
        _loops.Clear();
        foreach (var (uid, state) in _states)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
            {
                _done.Add(uid);
                continue;
            }

            var meta = MetaData(uid);
            if ((meta.Flags & MetaDataFlags.Detached) != 0)
            {
                state.Offsets.Clear();
                state.Scales.Clear();
                state.Rotations.Clear();
                state.Notifications.Clear();
            }
            else if (meta.EntityPaused)
                continue;

            UpdateOffset((uid, sprite), state, frameTime);
            UpdateScale((uid, sprite), state, frameTime);
            UpdateRotation((uid, sprite), state, frameTime);

            foreach (var (key, track) in state.Notifications)
            {
                if (track.Done)
                    _completed.Add((uid, key, track));
            }

            if ((meta.Flags & MetaDataFlags.Detached) == 0)
            {
                foreach (var (key, loop) in state.Loops)
                    _loops.Add((uid, key, loop));
            }

            if (!state.HasOffset && !state.HasScale && !state.HasRotation && state.Notifications.Count == 0 && state.Loops.Count == 0)
                _done.Add(uid);
        }

        foreach (var uid in _done)
            _states.Remove(uid);

        foreach (var (uid, key, track) in _completed)
        {
            if (!_states.TryGetValue(uid, out var state) ||
                !state.Notifications.TryGetValue(key, out var expected) || expected != track)
                continue;
            state.Notifications.Remove(key);
            var ev = new SpriteAnimationCompletedEvent(key);
            RaiseLocalEvent(uid, ref ev);
        }

        foreach (var (uid, key, loop) in _loops)
        {
            if (!_states.TryGetValue(uid, out var state) ||
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
            if (_states.TryGetValue(uid, out state) && TryComp<SpriteComponent>(uid, out var sprite))
            {
                UpdateOffset((uid, sprite), state, 0f);
                UpdateScale((uid, sprite), state, 0f);
                UpdateRotation((uid, sprite), state, 0f);
            }
        }
    }

    private static (Vector2 Value, float Time)[] ReadFrames(Animation animation, string property, Vector2 origin, out bool cubic)
    {
        if (animation.AnimationTracks.Count != 1 ||
            animation.AnimationTracks[0] is not AnimationTrackComponentProperty track ||
            track.ComponentType != typeof(SpriteComponent) || track.Property != property ||
            track.KeyFrames.Count < 2 ||
            track.InterpolationMode is not (AnimationInterpolationMode.Linear or AnimationInterpolationMode.Cubic))
            throw new NotSupportedException("expected a single sprite track with at least two keyframes");

        cubic = track.InterpolationMode == AnimationInterpolationMode.Cubic;
        var frames = new (Vector2 Value, float Time)[track.KeyFrames.Count];
        var duration = 0f;
        for (var i = 0; i < frames.Length; i++)
        {
            var frame = track.KeyFrames[i];
            if (frame.Value is not Vector2 value || frame.Easing != null || frame.KeyTime < 0f)
                throw new NotSupportedException("expected vector keyframes without easing and with non-negative times");
            frames[i] = (value - origin, i == 0 ? 0f : frame.KeyTime);
            duration += frames[i].Time;
        }

        var remaining = (float) animation.Length.TotalSeconds - duration;
        if (remaining < -0.001f)
            throw new NotSupportedException("animation length is shorter than its keyframes");
        if (remaining > 0.001f)
        {
            Array.Resize(ref frames, frames.Length + 1);
            frames[^1] = (frames[^2].Value, remaining);
        }
        return frames;
    }

    private void UpdateOffset(Entity<SpriteComponent> ent, SpriteAnimationState state, float frameTime)
    {
        if (!state.HasOffset)
            return;

        if (ent.Comp.Offset != state.LastOffset)
            state.Offset = ent.Comp.Offset;

        var offset = Vector2.Zero;
        state.Done.Clear();
        foreach (var (key, track) in state.Offsets)
        {
            track.Update(frameTime);
            if (track.Done && track.EndMode == SpriteAnimationEndMode.Release)
            {
                state.Done.Add(key);
                continue;
            }

            offset += track.Value;
        }

        foreach (var key in state.Done)
            state.Offsets.Remove(key);

        var result = state.Offset + offset;
        state.OffsetContribution = offset;
        _sprite.SetOffset(ent.AsNullable(), result);
        state.LastOffset = result;

        if (state.Offsets.Count == 0)
            state.HasOffset = false;
    }

    private void UpdateScale(Entity<SpriteComponent> ent, SpriteAnimationState state, float frameTime)
    {
        if (!state.HasScale)
            return;

        if (ent.Comp.Scale != state.LastScale)
            state.Scale = ent.Comp.Scale;

        var scale = Vector2.One;
        state.Done.Clear();
        foreach (var (key, track) in state.Scales)
        {
            track.Update(frameTime);
            if (track.Done && track.EndMode == SpriteAnimationEndMode.Release)
            {
                state.Done.Add(key);
                continue;
            }

            scale *= track.Value;
        }

        foreach (var key in state.Done)
            state.Scales.Remove(key);

        var result = state.Scale * scale;
        state.ScaleContribution = scale;
        _sprite.SetScale(ent.AsNullable(), result);
        state.LastScale = result;

        if (state.Scales.Count == 0)
            state.HasScale = false;
    }

    private void UpdateRotation(Entity<SpriteComponent> ent, SpriteAnimationState state, float frameTime)
    {
        if (!state.HasRotation)
            return;

        if (!ent.Comp.Rotation.Equals(state.LastRotation))
            state.Rotation = ent.Comp.Rotation;

        var rotation = 0f;
        state.Done.Clear();
        foreach (var (key, track) in state.Rotations)
        {
            track.Update(frameTime);
            if (track.Done && track.EndMode == SpriteAnimationEndMode.Release)
                state.Done.Add(key);
            else
                rotation += track.Value.X;
        }

        foreach (var key in state.Done)
            state.Rotations.Remove(key);

        state.RotationContribution = new Angle(rotation);
        state.LastRotation = state.Rotation + state.RotationContribution;
        _sprite.SetRotation(ent.AsNullable(), state.LastRotation);
        state.HasRotation = state.Rotations.Count != 0;
    }
}

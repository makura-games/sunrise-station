using System.Numerics;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._Sunrise.Animations.SpriteAnimation.Systems;

public sealed partial class SpriteAnimationSystem
{
    /// <summary>
    /// Возвращает базовый offset без анимаций. Внешняя запись в спрайт принимается за новую базу.
    /// </summary>
    public Vector2 GetBaseOffset(Entity<SpriteComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return Vector2.Zero;

        if (!_animationQuery.TryComp(ent.Owner, out var state) || !state.HasOffset)
            return ent.Comp.Offset;

        if (ent.Comp.Offset != state.LastOffset)
            state.Offset = ent.Comp.Offset;

        return state.Offset;
    }

    /// <summary>
    /// Меняет базовый offset и сразу накладывает поверх него активные анимации.
    /// </summary>
    public void SetBaseOffset(Entity<SpriteComponent?> ent, Vector2 offset)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (_animationQuery.TryComp(ent, out var state) && state.HasOffset)
        {
            state.Offset = offset;
            offset += state.OffsetContribution;
            state.LastOffset = offset;
        }

        _sprite.SetOffset(ent, offset);
    }

    /// <summary>
    /// Возвращает масштаб без множителей анимаций
    /// </summary>
    public Vector2 GetBaseScale(Entity<SpriteComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return Vector2.One;

        if (!_animationQuery.TryComp(ent.Owner, out var state) || !state.HasScale)
            return ent.Comp.Scale;

        if (ent.Comp.Scale != state.LastScale)
            state.Scale = ent.Comp.Scale;

        return state.Scale;
    }

    /// <summary>
    /// Меняет базовый scale и сразу накладывает поверх него активные множители.
    /// </summary>
    public void SetBaseScale(Entity<SpriteComponent?> ent, Vector2 scale)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (_animationQuery.TryComp(ent, out var state) && state.HasScale)
        {
            state.Scale = scale;
            scale *= state.ScaleContribution;
            state.LastScale = scale;
        }

        _sprite.SetScale(ent, scale);
    }

    /// <summary>
    /// Возвращает базовый rotation без анимаций. Внешняя запись в спрайт принимается за новую базу.
    /// </summary>
    public Angle GetBaseRotation(Entity<SpriteComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return Angle.Zero;

        if (!_animationQuery.TryComp(ent, out var state) || !state.HasRotation)
            return ent.Comp.Rotation;

        if (!ent.Comp.Rotation.Equals(state.LastRotation))
            state.Rotation = ent.Comp.Rotation;

        return state.Rotation;
    }

    /// <summary>
    /// Возвращает последний rotation-вклад указанного key, не двигая анимацию дальше.
    /// </summary>
    public Angle GetRotationOffset(Entity<SpriteComponent?> ent, string key)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return Angle.Zero;

        if (!_animationQuery.TryComp(ent, out var state) || !state.Rotations.TryGetValue(key, out var track))
            return Angle.Zero;

        return new Angle(track.Value);
    }

    /// <summary>
    /// Меняет базовый rotation и сразу накладывает поверх него активные анимации.
    /// </summary>
    public void SetBaseRotation(Entity<SpriteComponent?> ent, Angle rotation)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (_animationQuery.TryComp(ent, out var state) && state.HasRotation)
        {
            state.Rotation = rotation;
            rotation += state.RotationContribution;
            state.LastRotation = rotation;
        }

        _sprite.SetRotation(ent, rotation);
    }

    /// <summary>
    /// Проверяет, есть ли у key активный трек хотя бы в одном канале. Завершённый Hold тоже считается активным.
    /// Одна регистрация цикла без треков за проигрывание не считается.
    /// </summary>
    public bool IsPlaying(Entity<SpriteComponent?> ent, string key)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return false;

        return _animationQuery.TryComp(ent, out var state) &&
               (state.Offsets.ContainsKey(key) || state.Scales.ContainsKey(key) || state.Rotations.ContainsKey(key));
    }

    /// <summary>
    /// Проверяет регистрацию цикла, даже если сущность сейчас на паузе или вне PVS.
    /// </summary>
    public bool IsLooping(Entity<SpriteComponent?> ent, string key)
    {
        return Resolve(ent.Owner, ref ent.Comp) &&
               _animationQuery.TryComp(ent, out var state) && state.Loops.ContainsKey(key);
    }

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
    public void PlayLoop(Entity<SpriteComponent?> ent, string key, Action<EntityUid> play, Func<EntityUid, bool>? canPlay = null)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        var state = EnsureComp<SpriteAnimationComponent>(ent);
        state.Loops[key] = new SpriteAnimationLoop(play, canPlay);
        var meta = MetaData(ent);
        if (IsPaused(ent, meta))
            return;

        if (canPlay?.Invoke(ent) == false)
            Stop(ent, key);
        else if (!IsPlaying(ent, key))
            play(ent);
    }

    /// <summary>
    /// Запускает добавочную rotation-анимацию с Release в конце.
    /// </summary>
    public void PlayRotation(Entity<SpriteComponent?> ent, string key, params (Angle Value, float Time)[] frames)
        => PlayRotation(ent, key, false, frames);

    /// <summary>
    /// Заменяет rotation-вклад этого key линейной интерполяцией переданных углов.
    /// </summary>
    /// <remarks>
    /// Time у кадра — время от предыдущего кадра, Time первого кадра игнорируется.
    /// Углы интерполируются как переданы, без автоматического выбора короткой дуги.
    /// Release убирает вклад в конце, Hold оставляет его до замены или Stop.
    /// Меньше двух кадров и сущность без SpriteComponent игнорируются.
    /// </remarks>
    public void PlayRotation(
        Entity<SpriteComponent?> ent,
        string key,
        bool hold,
        params (Angle Value, float Time)[] frames)
    {
        if (!Resolve(ent.Owner, ref ent.Comp) || frames.Length < 2)
            return;

        var state = EnsureComp<SpriteAnimationComponent>(ent);

        if (!state.HasRotation)
        {
            state.Rotation = ent.Comp.Rotation;
            state.LastRotation = ent.Comp.Rotation;
            state.HasRotation = true;
        }

        var values = new (float Value, float Time)[frames.Length];
        for (var i = 0; i < frames.Length; i++)
            values[i] = ((float) frames[i].Value.Theta, frames[i].Time);

        state.Rotations[key] = new SpriteAnimationTrack<float>(values, false, hold);
    }

    /// <summary>
    /// Переводит один трек SpriteComponent.Offset в добавочный offset этой системы
    /// </summary>
    /// <remarks>
    /// origin вычитается из значений кадров, а не из текущего offset спрайта.
    /// Первый кадр берётся из прошлого вклада этого key, либо из нуля.
    /// Нужно минимум два Vector2 keyframe с неотрицательным временем, easing выполняет движок
    /// Length может быть длиннее кадров, но не короче их общей длины.
    /// Пустая анимация и сущность без SpriteComponent игнорируются
    /// Неподдерживаемый формат логируется, ключ снимается вместе с регистрацией цикла
    /// </remarks>
    public void PlayOffset(
        Entity<SpriteComponent?> ent,
        Animation animation,
        string key,
        Vector2 origin,
        bool hold = false)
    {
        if (animation.AnimationTracks.Count == 0 || !Resolve(ent.Owner, ref ent.Comp))
            return;

        if (!TryReadFrames(animation, nameof(SpriteComponent.Offset), origin, out var frames, out var mode))
        {
            Stop(ent, key);
            return;
        }

        var from = _animationQuery.TryComp(ent, out var state) && state.Offsets.TryGetValue(key, out var previous)
            ? previous.Value
            : Vector2.Zero;
        frames[0] = new AnimationTrackProperty.KeyFrame(from, 0f);
        var track = new SpriteAnimationTrack<Vector2>(frames, mode, hold);
        SetOffsetTrack((ent.Owner, ent.Comp), key, track);
    }

    /// <summary>
    /// Запускает добавочную offset-анимацию с Release в конце.
    /// </summary>
    public void PlayOffset(Entity<SpriteComponent?> ent, string key, bool cubic, params (Vector2 Value, float Time)[] frames)
        => PlayOffset(ent, key, cubic, false, frames);

    /// <summary>
    /// Заменяет offset-вклад этого key. Интерполяция cubic либо linear, в зависимости от параметра.
    /// </summary>
    /// <remarks>
    /// Значения — это вклад поверх базы, а не абсолютный offset. Time считается от предыдущего кадра.
    /// Time первого кадра игнорируется. Release убирает вклад в конце, Hold оставляет его.
    /// Меньше двух кадров и сущность без SpriteComponent игнорируются.
    /// </remarks>
    public void PlayOffset(
        Entity<SpriteComponent?> ent,
        string key,
        bool cubic,
        bool hold,
        params (Vector2 Value, float Time)[] frames)
    {
        if (!Resolve(ent.Owner, ref ent.Comp) || frames.Length < 2)
            return;

        SetOffsetTrack((ent.Owner, ent.Comp), key, new SpriteAnimationTrack<Vector2>(frames, cubic, hold));
    }

    /// <summary>
    /// Переводит трек SpriteComponent.Scale в множители масштаба
    /// </summary>
    /// <remarks>
    /// Значения считаются множителями относительно базы, а не абсолютным scale спрайта.
    /// Пустая анимация и сущность без SpriteComponent игнорируются.
    /// </remarks>
    public void PlayScale(Entity<SpriteComponent?> ent, Animation animation, string key,
        bool hold = false)
    {
        if (animation.AnimationTracks.Count == 0 || !Resolve(ent.Owner, ref ent.Comp))
            return;

        if (!TryReadFrames(animation, nameof(SpriteComponent.Scale), Vector2.Zero, out var frames, out var mode))
        {
            Stop(ent, key);
            return;
        }

        var track = new SpriteAnimationTrack<Vector2>(frames, mode, hold);
        SetScaleTrack((ent.Owner, ent.Comp), key, track);
    }

    /// <summary>
    /// Запускает анимацию множителя scale с Release в конце.
    /// </summary>
    public void PlayScale(Entity<SpriteComponent?> ent, string key, params (Vector2 Value, float Time)[] frames)
        => PlayScale(ent, key, false, frames);

    /// <summary>
    /// Заменяет scale-вклад этого key с линейной интерполяцией множителей.
    /// </summary>
    /// <remarks>
    /// Vector2.One не меняет базовый scale. Time считается от предыдущего кадра, Time первого игнорируется.
    /// Release убирает множитель в конце, Hold оставляет его до замены или Stop.
    /// Меньше двух кадров и сущность без SpriteComponent игнорируются.
    /// </remarks>
    public void PlayScale(
        Entity<SpriteComponent?> ent,
        string key,
        bool hold,
        params (Vector2 Value, float Time)[] frames)
    {
        if (!Resolve(ent.Owner, ref ent.Comp) || frames.Length < 2)
            return;

        SetScaleTrack((ent.Owner, ent.Comp), key, new SpriteAnimationTrack<Vector2>(frames, false, hold));
    }

    /// <summary>
    /// Перематывает все треки этого key на указанное время от начала. Отрицательное время считается нулём.
    /// </summary>
    /// <remarks>
    /// Можно мотать назад. Отсутствующие треки игнорируются, спрайт обновится при следующей сборке кадра.
    /// </remarks>
    public void Seek(Entity<SpriteComponent?> ent, string key, float elapsed)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (!_animationQuery.TryComp(ent, out var state))
            return;

        if (state.Offsets.TryGetValue(key, out var offset))
            offset.Seek(elapsed);

        if (state.Scales.TryGetValue(key, out var scale))
            scale.Seek(elapsed);

        if (state.Rotations.TryGetValue(key, out var rotation))
            rotation.Seek(elapsed);
    }

    /// <summary>
    /// Убирает треки и регистрацию цикла указанного ключа
    /// Спрайт сразу пересобирается без этого вклада
    /// </summary>
    public void Stop(Entity<SpriteComponent?> ent, string key)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (!_animationQuery.TryComp(ent, out var state))
            return;

        state.Offsets.Remove(key);
        state.Scales.Remove(key);
        state.Rotations.Remove(key);
        state.Loops.Remove(key);

        ApplyContributions((ent.Owner, ent.Comp), state, 0f);
    }

    /// <summary>
    /// Задаёт постоянный добавочный вклад под отдельным ключом
    /// </summary>
    public void SetOffsetContribution(Entity<SpriteComponent?> ent, string key, Vector2 offset)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        SetOffsetTrack((ent.Owner, ent.Comp), key, new SpriteAnimationTrack<Vector2>([(offset, 0f), (offset, 0f)], false, true));
        ApplyContributions((ent.Owner, ent.Comp), Comp<SpriteAnimationComponent>(ent), 0f);
    }

    /// <summary>
    /// Задаёт постоянный множитель масштаба под отдельным ключом
    /// </summary>
    public void SetScaleContribution(Entity<SpriteComponent?> ent, string key, Vector2 scale)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        SetScaleTrack((ent.Owner, ent.Comp), key, new SpriteAnimationTrack<Vector2>([(scale, 0f), (scale, 0f)], false, true));
        ApplyContributions((ent.Owner, ent.Comp), Comp<SpriteAnimationComponent>(ent), 0f);
    }

    /// <summary>
    /// Возвращает текущий множитель ключа или единицу, если ключ отсутствует
    /// </summary>
    public Vector2 GetScaleMultiplier(Entity<SpriteComponent?> ent, string key)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return Vector2.One;

        return _animationQuery.TryComp(ent, out var state) && state.Scales.TryGetValue(key, out var track)
            ? track.Value
            : Vector2.One;
    }

    private void SetOffsetTrack(Entity<SpriteComponent> ent, string key, SpriteAnimationTrack<Vector2> track)
    {
        var state = EnsureComp<SpriteAnimationComponent>(ent);
        if (!state.HasOffset)
        {
            state.Offset = ent.Comp.Offset;
            state.LastOffset = ent.Comp.Offset;
            state.HasOffset = true;
        }

        state.Offsets[key] = track;
    }

    private void SetScaleTrack(Entity<SpriteComponent> ent, string key, SpriteAnimationTrack<Vector2> track)
    {
        var state = EnsureComp<SpriteAnimationComponent>(ent);
        if (!state.HasScale)
        {
            state.Scale = ent.Comp.Scale;
            state.LastScale = ent.Comp.Scale;
            state.HasScale = true;
        }

        state.Scales[key] = track;
    }

    private bool TryReadFrames(
        Animation animation,
        string property,
        Vector2 origin,
        out List<AnimationTrackProperty.KeyFrame> frames,
        out AnimationInterpolationMode mode)
    {
        frames = new List<AnimationTrackProperty.KeyFrame>();
        mode = AnimationInterpolationMode.Linear;
        if (animation.AnimationTracks.Count != 1 ||
            animation.AnimationTracks[0] is not AnimationTrackComponentProperty track ||
            track.ComponentType != typeof(SpriteComponent) || track.Property != property ||
            track.KeyFrames.Count < 2 ||
            track.InterpolationMode is not (AnimationInterpolationMode.Linear or AnimationInterpolationMode.Cubic or
                AnimationInterpolationMode.Nearest or AnimationInterpolationMode.Previous))
        {
            Log.Error($"unsupported sprite animation track for {property}");
            return false;
        }

        mode = track.InterpolationMode;
        var duration = 0f;
        for (var i = 0; i < track.KeyFrames.Count; i++)
        {
            var frame = track.KeyFrames[i];
            if (frame.Value is not Vector2 value || !float.IsFinite(frame.KeyTime) || frame.KeyTime < 0f)
            {
                Log.Error($"invalid sprite animation keyframe for {property}");
                return false;
            }

            var time = i == 0 ? 0f : frame.KeyTime;
            frames.Add(new AnimationTrackProperty.KeyFrame(value - origin, time, frame.Easing));
            duration += time;
        }

        var remaining = (float) animation.Length.TotalSeconds - duration;
        if (!float.IsFinite(remaining) || remaining < -0.001f)
        {
            Log.Error($"sprite animation length is shorter than its keyframes for {property}");
            return false;
        }

        if (remaining > 0.001f)
            frames.Add(new AnimationTrackProperty.KeyFrame(frames[^1].Value, remaining));

        return true;
    }
}

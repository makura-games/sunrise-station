using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Client._Sunrise.Animations;

internal sealed class SpriteAnimationTrack(
    (Vector2 Value, float Time)[] frames,
    bool cubic,
    SpriteAnimationEndMode endMode)
{
    private int _index;
    private float _time;

    public bool Done { get; private set; }
    public Vector2 Value { get; private set; } = frames[0].Value;
    public readonly SpriteAnimationEndMode EndMode = endMode;

    public void Seek(float elapsed)
    {
        _index = 0;
        _time = 0f;
        Done = false;
        Update(MathF.Max(0f, elapsed));
    }

    public void Update(float frameTime)
    {
        if (Done)
            return;

        _time += frameTime;

        while (_index < frames.Length - 1 && _time >= frames[_index + 1].Time)
        {
            var time = frames[_index + 1].Time;
            if (time > 0f)
                _time -= time;

            _index++;
            if (_index == frames.Length - 1)
            {
                Done = true;
                Value = frames[_index].Value;
                return;
            }
        }

        var next = _index + 1;
        var duration = frames[next].Time;
        var t = duration <= 0f ? 1f : Math.Clamp(_time / duration, 0f, 1f);

        if (!cubic)
        {
            Value = Vector2.Lerp(frames[_index].Value, frames[next].Value, t);
            return;
        }

        var prev = _index > 0 ? _index - 1 : _index;
        var post = next < frames.Length - 1 ? next + 1 : next;
        Value = Vector2Helpers.InterpolateCubic(
            frames[prev].Value,
            frames[_index].Value,
            frames[next].Value,
            frames[post].Value,
            t);
    }
}

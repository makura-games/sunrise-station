using Robust.Client.Animations;
using Robust.Shared.Animations;

namespace Content.Client._Sunrise.Animations.SpriteAnimation;

internal sealed class SpriteAnimationTrack<T> : AnimationTrackProperty where T : struct
{
    private int _index;
    private float _time;

    public bool Done { get; private set; }
    public T Value { get; private set; }
    public readonly bool Hold;

    public SpriteAnimationTrack(
        (T Value, float Time)[] frames,
        bool cubic,
        bool hold)
    {
        foreach (var (value, time) in frames)
            KeyFrames.Add(new KeyFrame(value, time));

        InterpolationMode = cubic ? AnimationInterpolationMode.Cubic : AnimationInterpolationMode.Linear;
        Hold = hold;
        Value = frames[0].Value;
    }

    public SpriteAnimationTrack(List<KeyFrame> frames, AnimationInterpolationMode mode, bool hold)
    {
        KeyFrames = frames;
        InterpolationMode = mode;
        Hold = hold;
        Value = (T) frames[0].Value;
    }

    public void Update(float frameTime)
    {
        if (Done)
            return;

        _time += frameTime;

        // РќСѓР»РµРІС‹Рµ РєР°РґСЂС‹ Рё С‚РѕС‡РЅР°СЏ РіСЂР°РЅРёС†Р° Р·Р°РІРµСЂС€Р°СЋС‚СЃСЏ РґРѕ РёРЅС‚РµСЂРїРѕР»СЏС†РёРё, Р±РµР· РґРµР»РµРЅРёСЏ РЅР° РЅРѕР»СЊ
        while (_index < KeyFrames.Count - 1 && _time >= KeyFrames[_index + 1].KeyTime)
        {
            var time = KeyFrames[_index + 1].KeyTime;
            if (time > 0f)
                _time -= time;

            _index++;
            if (_index == KeyFrames.Count - 1)
            {
                Done = true;
                Value = (T) KeyFrames[_index].Value;
                return;
            }
        }

        base.AdvancePlayback(this, _index, _time, 0f);
    }

    public void Seek(float elapsed)
    {
        _index = 0;
        _time = 0f;
        Done = false;
        Update(MathF.Max(0f, elapsed));
    }

    protected override void ApplyProperty(object context, object value)
    {
        Value = (T) value;
    }
}

using System.Numerics;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Animations.SpriteAnimation.Systems;

public sealed partial class SpriteAnimationSystem
{
    private void ApplyContributions(Entity<SpriteComponent> ent, SpriteAnimationComponent state, float frameTime)
    {
        if (state.HasOffset)
        {
            if (ent.Comp.Offset != state.LastOffset)
                state.Offset = ent.Comp.Offset;

            state.OffsetContribution = UpdateChannel(state.Offsets, state.Done, frameTime, Vector2.Zero, (a, b) => a + b);
            state.LastOffset = state.Offset + state.OffsetContribution;
            _sprite.SetOffset(ent.AsNullable(), state.LastOffset);
            state.HasOffset = state.Offsets.Count != 0;
        }

        if (state.HasScale)
        {
            if (ent.Comp.Scale != state.LastScale)
                state.Scale = ent.Comp.Scale;

            state.ScaleContribution = UpdateChannel(state.Scales, state.Done, frameTime, Vector2.One, (a, b) => a * b);
            state.LastScale = state.Scale * state.ScaleContribution;
            _sprite.SetScale(ent.AsNullable(), state.LastScale);
            state.HasScale = state.Scales.Count != 0;
        }

        if (state.HasRotation)
        {
            if (!ent.Comp.Rotation.Equals(state.LastRotation))
                state.Rotation = ent.Comp.Rotation;

            state.RotationContribution = new Angle(UpdateChannel(state.Rotations, state.Done, frameTime, 0f, (a, b) => a + b));
            state.LastRotation = state.Rotation + state.RotationContribution;
            _sprite.SetRotation(ent.AsNullable(), state.LastRotation);
            state.HasRotation = state.Rotations.Count != 0;
        }
    }

    private static T UpdateChannel<T>(Dictionary<string, SpriteAnimationTrack<T>> tracks, List<string> done,
        float frameTime, T identity, Func<T, T, T> combine) where T : struct
    {
        var contribution = identity;
        done.Clear();
        foreach (var (key, track) in tracks)
        {
            track.Update(frameTime);
            if (track.Done && !track.Hold)
                done.Add(key);
            else
                contribution = combine(contribution, track.Value);
        }

        foreach (var key in done)
            tracks.Remove(key);

        return contribution;
    }
}

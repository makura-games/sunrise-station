using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._Sunrise.Animations;

internal sealed class SpriteAnimationState
{
    public readonly Dictionary<string, SpriteAnimationTrack> Offsets = new();
    public readonly Dictionary<string, SpriteAnimationTrack> Scales = new();
    public readonly Dictionary<string, SpriteAnimationTrack> Rotations = new();
    public readonly Dictionary<string, SpriteAnimationTrack> Notifications = new();
    public readonly Dictionary<string, SpriteAnimationLoop> Loops = new();
    public readonly List<string> Done = new();
    public Vector2 Offset;
    public Vector2 LastOffset;
    public Vector2 Scale = Vector2.One;
    public Vector2 LastScale = Vector2.One;
    public Vector2 OffsetContribution;
    public Vector2 ScaleContribution = Vector2.One;
    public Angle Rotation;
    public Angle LastRotation;
    public Angle RotationContribution;
    public bool HasOffset;
    public bool HasScale;
    public bool HasRotation;
}

internal sealed record SpriteAnimationLoop(Action<EntityUid> Play, Func<EntityUid, bool>? CanPlay);

namespace Content.Client._Sunrise.Animations;

/// <summary>
/// controls whether a finished track keeps contributing to the sprite
/// </summary>
public enum SpriteAnimationEndMode : byte
{
    /// <summary>
    /// removes the finished track's contribution, preserving the base and other effects
    /// </summary>
    Release,
    /// <summary>
    /// keeps the final contribution until replaced, stopped or reset on leaving PVS
    /// </summary>
    Hold,
}

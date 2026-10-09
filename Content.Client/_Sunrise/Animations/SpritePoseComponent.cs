namespace Content.Client._Sunrise.Animations;

[RegisterComponent, Access(typeof(SpritePoseSystem))]
public sealed partial class SpritePoseComponent : Component
{
    /// <summary>
    /// whether the target pose has been applied since the last PVS or carrying reset
    /// </summary>
    public bool HasPose;

    /// <summary>
    /// last applied target angle, excluding transient animation contributions
    /// </summary>
    public Angle Rotation;

    /// <summary>
    /// pose used when no override is active
    /// </summary>
    public Angle BaseRotation;

    /// <summary>
    /// temporary pose taking priority over BaseRotation until cleared
    /// </summary>
    public Angle? OverrideRotation;
}

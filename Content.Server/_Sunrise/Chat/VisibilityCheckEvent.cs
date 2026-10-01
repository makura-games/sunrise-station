using NetCord;
namespace Content.Server._Sunrise.Chat;

[ByRefEvent]
public struct EmoteVisibilityCheckEvent(EntityUid source, EntityUid? target, float range)
{
    public EntityUid Source { get; } = source;

    public EntityUid? Target { get; } = target;

    public float Range { get; } = range;

    public bool Visible { get; set; } = true;
}

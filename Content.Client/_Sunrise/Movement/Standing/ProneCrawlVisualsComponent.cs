namespace Content.Client._Sunrise.Movement.Standing;

[RegisterComponent, Access(typeof(ProneCrawlVisualsSystem))]
public sealed partial class ProneCrawlVisualsComponent : Component
{
    public bool Prone;
    public TimeSpan PullEnd;
    public bool HadOverride;
    public Direction Direction;
}

using System.Numerics;

#pragma warning disable IDE0130
namespace Content.Shared.Stunnable;

public sealed partial class CrawlerComponent
{
    /// <summary>
    /// maximum distance covered by a single crawl pull
    /// </summary>
    [DataField, AutoNetworkedField, Access(typeof(Content.Shared._Sunrise.Movement.Standing.ProneCrawlMovementController))]
    public float PullDistance = 0.7f;

    /// <summary>
    /// duration of a single pull
    /// </summary>
    [DataField, AutoNetworkedField, Access(typeof(Content.Shared._Sunrise.Movement.Standing.ProneCrawlMovementController))]
    public TimeSpan PullDuration = TimeSpan.FromSeconds(0.25f);

    /// <summary>
    /// pause between pulls
    /// </summary>
    [DataField, AutoNetworkedField, Access(typeof(Content.Shared._Sunrise.Movement.Standing.ProneCrawlMovementController))]
    public TimeSpan PullPause = TimeSpan.FromSeconds(0.3f);

    /// <summary>
    /// sprite offset amplitude during a pull
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PullOffset = 0.08f;

    /// <summary>
    /// sprite scale factors at the peak of a pull
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 PullScale = new(1.05f, 0.95f);
}

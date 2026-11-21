using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Events;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems;

public abstract partial class SharedTutorialSystem
{
    private void ClearTracking(Entity<TutorialPlayerComponent> ent)
    {
        if (!_net.IsServer)
            return;

        var trackingEnded = new TutorialTrackingEndedEvent(ent);
        RaiseLocalEvent(ref trackingEnded);
    }
}

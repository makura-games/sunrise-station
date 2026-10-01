using Content.Shared._Sunrise.Mood;

namespace Content.Shared.Bible;

public sealed partial class BibleSystem
{
    private void OnSunriseBibleUsed(EntityUid target)
    {
        if (_net.IsClient)
            return;

        RaiseLocalEvent(target, new MoodEffectEvent("GotBlessed"));
    }
}

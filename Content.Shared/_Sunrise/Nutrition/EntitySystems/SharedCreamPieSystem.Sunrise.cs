using Content.Shared._Sunrise.Mood;

namespace Content.Shared.Nutrition.EntitySystems;

public abstract partial class SharedCreamPieSystem
{
    private void OnCreamPiedChanged(EntityUid target, bool value)
    {
        if (!value)
        {
            RaiseLocalEvent(target, new MoodRemoveEffectEvent("Creampied"));
            return;
        }

        RaiseLocalEvent(target, new MoodEffectEvent("Creampied"));
    }

    private void RaiseSunriseCreamedEvent(EntityUid target)
    {
        var creamedEvent = new CreamedEvent(target);
        RaiseLocalEvent(target, ref creamedEvent);
    }
}

[ByRefEvent]
public readonly record struct CreamedEvent(EntityUid Target);

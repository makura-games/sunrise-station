using Content.Server.Vocalization.Components;

namespace Content.Server.Vocalization.Systems;

public sealed partial class DatasetVocalizationSystem
{
    private void InitializeSunrise()
    {
        SubscribeLocalEvent<DatasetVocalizerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DatasetVocalizerComponent> ent, ref MapInitEvent args)
    {
        var vocalizer = EnsureComp<VocalizerComponent>(ent);

        if (ent.Comp.MinVocalizeInterval is { } min)
            vocalizer.MinVocalizeInterval = min;

        if (ent.Comp.MaxVocalizeInterval is { } max)
            vocalizer.MaxVocalizeInterval = max;

        if (vocalizer.MaxVocalizeInterval < vocalizer.MinVocalizeInterval)
            vocalizer.MaxVocalizeInterval = vocalizer.MinVocalizeInterval;

        if (ent.Comp.HideChat is { } hideChat)
            vocalizer.HideChat = hideChat;
    }
}

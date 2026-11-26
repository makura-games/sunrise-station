using System.Diagnostics.CodeAnalysis;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Humanoid;

namespace Content.Shared._Sunrise.Mood;

public sealed partial class MoodVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SunriseHumanoidBodySystem _body = default!;

    [SubscribeLocalEvent]
    public void OnStartup(Entity<MoodVisualsComponent> ent, ref ComponentStartup args)
        => QueueUpdate((ent, ent.Comp));

    [SubscribeLocalEvent]
    public void OnAfterAutoHandleState(Entity<MoodVisualsComponent> ent, ref AfterAutoHandleStateEvent args)
        => QueueUpdate((ent, ent.Comp));

    [SubscribeLocalEvent]
    public void OnLayerVisibilityChanged(Entity<MoodVisualsComponent> ent, ref HumanoidLayerVisibilityChangedEvent args)
        => QueueUpdate((ent, ent.Comp));

    public void QueueUpdate(Entity<MoodVisualsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        _appearance.QueueUpdate(ent, appearance);
    }

    public bool TryGetMoodState(
        Entity<MoodVisualsComponent> ent,
        AppearanceComponent appearance,
        HumanoidVisualLayers layer,
        string defaultState,
        [NotNullWhen(true)] out string? state)
    {
        state = null;
        if (!_body.IsLayerVisible(ent, layer))
            return false;

        state = defaultState;
        return _appearance.TryGetData<MoodThreshold>(ent, MoodVisuals.CurrentMoodThreshold, out var threshold, appearance)
            ? ent.Comp.MoodStates.TryGetValue(threshold, out state)
            : ent.Comp.VisibleWithoutMood;
    }
}

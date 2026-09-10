using Content.Shared.Nutrition.Components;
using Robust.Shared.GameObjects;

namespace Content.Shared.Nutrition.EntitySystems;

public sealed class HungerManglenessChangedEvent : EntityEventArgs
{
    public bool HasMangleness { get; }

    public HungerManglenessChangedEvent(bool hasMangleness)
    {
        HasMangleness = hasMangleness;
    }
}

public sealed class ThirstManglenessChangedEvent : EntityEventArgs
{
    public bool HasMangleness { get; }

    public ThirstManglenessChangedEvent(bool hasMangleness)
    {
        HasMangleness = hasMangleness;
    }
}

public sealed class HungerDecayRateModifierEvent : EntityEventArgs
{
    public HungerComponent Component { get; }
    public float ActualDecayRate { get; set; }

    public HungerDecayRateModifierEvent(HungerComponent component, float actualDecayRate)
    {
        Component = component;
        ActualDecayRate = actualDecayRate;
    }
}

public sealed class ThirstDecayRateModifierEvent : EntityEventArgs
{
    public ThirstComponent Component { get; }
    public float ActualDecayRate { get; set; }

    public ThirstDecayRateModifierEvent(ThirstComponent component, float actualDecayRate)
    {
        Component = component;
        ActualDecayRate = actualDecayRate;
    }
}

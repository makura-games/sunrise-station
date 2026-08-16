using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Antags.Vampires.Events;

public sealed partial class VampireGlareActionEvent : InstantActionEvent;

public sealed partial class VampireSleepActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class VampireSleepDoAfterEvent : SimpleDoAfterEvent
{
    [DataField(required: true)]
    public NetEntity Victim;

    [DataField(required: true)]
    public NetEntity Action;

    [DataField]
    public float MaxDistance = 2.5f;

    [DataField]
    public int BloodCost = 20;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    [DataField]
    public bool IgnoresFaith;
}

[Serializable, NetSerializable]
public sealed partial class VampireDrinkBloodDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class VampireDevourDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public float BloodFullnessRestore;
}

public sealed partial class VampireRejuvenateIActionEvent : InstantActionEvent;

public sealed partial class VampireRejuvenateIIActionEvent : InstantActionEvent;

public sealed partial class VampireToggleFangsActionEvent : InstantActionEvent;

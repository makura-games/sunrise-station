using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.FleshCult;

public sealed partial class FleshCultistNightVisionMutationEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistInsulatedImmunityMutationEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistPressureImmunityMutationEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistFlashImmunityMutationEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistRespiratorImmunityMutationEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistColdTempImmunityMutationEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistShopActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistAdrenalinActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistCreateFleshHeartActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistAbsorbBloodPoolActionEvent : InstantActionEvent
{

}

public sealed partial class FleshCultistDevourActionEvent : EntityTargetActionEvent
{

}

public sealed partial class FleshCultistThrowHuggerActionEvent : WorldTargetActionEvent
{
}

public sealed partial class FleshCultistAcidSpitActionEvent : WorldTargetActionEvent
{

}

[Serializable, NetSerializable]
public sealed partial class FleshCultistDevourDoAfterEvent : SimpleDoAfterEvent
{

}


public sealed partial class FleshCultistHandTransformEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId Prototype;
}

public sealed partial class FleshCultistBodyTransformEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId Prototype;
    [DataField]
    public string TargetSlot = string.Empty;
    [DataField]
    public List<string> CheckSlots = [];
    [DataField]
    public List<ProtoId<TagPrototype>> CheckTags = [];
    [DataField]
    public List<string> UnequipSlots = [];
}

public sealed partial class FleshCultistUnlockAbilityEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId Prototype = string.Empty;
}

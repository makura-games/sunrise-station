using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Flyswatter;

[RegisterComponent]
public sealed partial class FlyswatterComponent : Component
{
    /// <summary>
    /// Во сколько раз увеличить урон по насекомым.
    /// </summary>
    [DataField]
    public float InsectDamageMultiplier = 8f;

    /// <summary>
    /// Реагент крови, по которому считаем цель насекомым
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> InsectBloodReagent = "InsectBlood";
}


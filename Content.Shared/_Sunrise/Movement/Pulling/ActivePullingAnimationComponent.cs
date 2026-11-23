using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Movement.Pulling;

/// <summary>
/// Добавляется, пока у сущности активен визуальный эффект притягивания.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedPullingAnimationSystem))]
public sealed partial class ActivePullingAnimationComponent : Component
{
    /// <summary>
    /// Визуальный эффект, который прикрепляется к притягиваемой сущности.
    /// </summary>
    [DataField]
    public EntProtoId EffectPrototype = "SunriseEffectGrab";

    /// <summary>
    /// Звук начала притягивания.
    /// </summary>
    [DataField]
    public SoundSpecifier PullSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg")
    {
        Params = AudioParams.Default.WithVariation(0.05f),
    };

    /// <summary>
    /// Визуальный эффект, прикрепленный к притягиваемой сущности.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Effect;
}

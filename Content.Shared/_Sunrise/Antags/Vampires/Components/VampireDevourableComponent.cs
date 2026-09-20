using Robust.Shared.Audio;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Помечает сущность как потребляемую вампирами через UseInHand
/// </summary>
[RegisterComponent]
public sealed partial class VampireDevourableComponent : Component
{
    /// <summary>
    /// Длительность doafter потребления сущности.
    /// </summary>
    [DataField]
    public TimeSpan DevourDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Сколько <see cref="VampireBloodDrinkerComponent.BloodFullness"/> восстанавливается при потреблении
    /// </summary>
    [DataField]
    public float BloodFullnessRestore = 25f;

    /// <summary>
    /// Звук потребления сущности.
    /// </summary>
    [DataField]
    public SoundSpecifier DevourSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");
}

using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Effects;

/// <summary>
///     Base data type for effects applied while a tutorial step is active.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class TutorialEffect
{
}

/// <summary>
///     Applies a status effect to the tutorial player for the active step.
/// </summary>
public sealed partial class TutorialStatusEffect : TutorialEffect
{
    /// <summary>
    ///     Status effect prototype to apply.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Status;

    /// <summary>
    ///     Optional status effect duration. If unset, the status is permanent until this step removes it.
    /// </summary>
    [DataField]
    public TimeSpan? Duration;

    /// <summary>
    ///     If true, the status effect is removed when the step stops being active.
    /// </summary>
    [DataField]
    public bool RemoveOnExit = true;
}

/// <summary>
///     Adds components to the tutorial player for the active step.
/// </summary>
public sealed partial class TutorialComponentEffect : TutorialEffect
{
    /// <summary>
    ///     Components to add.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    /// <summary>
    ///     If true, existing components with the same type are replaced.
    /// </summary>
    [DataField]
    public bool RemoveExisting = true;

    /// <summary>
    ///     If true, added components are removed when the step stops being active.
    /// </summary>
    [DataField]
    public bool RemoveOnExit = true;
}

/// <summary>
///     Снимает компоненты с игрока при активации шага туториала.
/// </summary>
public sealed partial class TutorialRemoveComponentEffect : TutorialEffect
{
    /// <summary>
    ///     Компоненты, которые необходимо снять.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry Components = new();
}

/// <summary>
///     Applies one-shot entity effects when the step becomes active.
/// </summary>
public sealed partial class TutorialEntityEffect : TutorialEffect
{
    /// <summary>
    ///     Effects to apply to the tutorial player.
    /// </summary>
    [DataField(required: true)]
    public EntityEffect[] Effects = [];

    /// <summary>
    ///     Scale passed to the entity effect system.
    /// </summary>
    [DataField]
    public float Scale = 1f;
}

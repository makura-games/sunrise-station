using Robust.Shared.Audio;

namespace Content.Server._Sunrise.GameTicking.Rules.Components;

[RegisterComponent]
public sealed partial class VampireRuleComponent : Component
{
    public readonly List<EntityUid> VampireMinds = new();

    /// <summary>
    /// Звук брифинга при назначении роли вампира.
    /// </summary>
    [DataField]
    public SoundSpecifier BriefingSound = new SoundPathSpecifier("/Audio/_Sunrise/Ambience/Antag/vampire_start.ogg");
}

using Content.Shared.AbstractAnalyzer;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Medical.Components;

/// <summary>
/// After scanning, retrieves the target Uid to use with its related UI.
/// </summary>
/// <remarks>
/// Requires <c>ItemToggleComponent</c>.
/// </remarks>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(HealthAnalyzerSystem), typeof(CryoPodSystem))]
public sealed partial class HealthAnalyzerComponent : AbstractAnalyzerComponent
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public override TimeSpan NextUpdate { get; set; } = TimeSpan.Zero;
}


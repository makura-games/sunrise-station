using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.Borgs.Components;

[RegisterComponent]
public sealed partial class BorgCrewManifestViewerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId ActionViewCrewManifest = "ActionViewCrewManifest";
}

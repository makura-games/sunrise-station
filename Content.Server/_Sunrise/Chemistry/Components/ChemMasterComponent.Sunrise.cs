using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Chemistry.Components
{
    public sealed partial class ChemMasterComponent
    {
        // Sunrise-Edit start - patch creation limits
        [DataField("patchDosageLimit"), ViewVariables(VVAccess.ReadWrite)]
        public uint PatchDosageLimit = 20;
        // Sunrise-Edit end
    }
}

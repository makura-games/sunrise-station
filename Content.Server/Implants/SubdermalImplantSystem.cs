using Content.Shared.Implants;

namespace Content.Server.Implants;

public sealed partial class SubdermalImplantSystem : SharedSubdermalImplantSystem
{
    public override void Initialize()
    {
        base.Initialize();
        InitializeStarlight();
    }
}

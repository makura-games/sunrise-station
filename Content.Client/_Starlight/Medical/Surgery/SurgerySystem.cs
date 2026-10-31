using Content.Shared.Body;
using Content.Shared.Starlight.Medical.Surgery;
using Content.Shared.Starlight.Medical.Surgery.Effects.Step;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
public sealed partial class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryProgressComponent, AfterAutoHandleStateEvent>(OnProgressState);
    }

    private void OnProgressState(Entity<SurgeryProgressComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<OrganComponent>(ent, out var organ) ||
            organ.Body is not { } body ||
            !_ui.TryGetOpenUi(body, SurgeryUIKey.Key, out var bui))
        {
            return;
        }

        bui.Update();
    }
}

using Content.Shared._Sunrise.MindBearer;
using Content.Server.Ghost.Roles.Raffles;
using Content.Server.Ghost.Roles.Components;

namespace Content.Server._Sunrise.MindBearer;

public sealed partial class MindBearerSystem : SharedMindBearerSystem
{
    protected override void OnMindBearerDoAfter(Entity<MindBearerComponent> ent, ref MindBearerDoAfterEvent args)
    {
        base.OnMindBearerDoAfter(ent, ref args);

        if (args.Cancelled)
            return;

        var oldEntity = args.Args.User;
        var entMeta = MetaData(oldEntity);
        var ghostRoleComp = EnsureComp<GhostRoleComponent>(oldEntity);
        EnsureComp<GhostTakeoverAvailableComponent>(oldEntity);
        ghostRoleComp.RoleName = entMeta.EntityName;
        ghostRoleComp.RoleDescription = entMeta.EntityDescription;
        ghostRoleComp.RaffleConfig = new GhostRoleRaffleConfig(ent.Comp.GhostRoleSettings);
        args.Handled = true;
    }
}

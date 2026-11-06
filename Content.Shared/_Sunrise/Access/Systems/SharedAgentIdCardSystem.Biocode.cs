using Content.Shared._Sunrise.Biocode;
using Content.Shared.Access.Components;

#pragma warning disable IDE0130
namespace Content.Shared.Access.Systems;

public abstract partial class SharedAgentIdCardSystem
{
    [Dependency] private BiocodeSystem _biocode = default!;

    private bool CanCopySunriseAccess(Entity<AgentIDCardComponent> ent, EntityUid user)
    {
        return !TryComp<BiocodeComponent>(ent, out var biocode) ||
               _biocode.CanUse(user, biocode.Factions);
    }
}

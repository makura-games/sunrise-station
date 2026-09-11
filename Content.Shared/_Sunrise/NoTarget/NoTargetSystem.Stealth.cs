using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;

namespace Content.Shared._Sunrise.NoTarget;

public sealed partial class NpcNoTargetSystem
{
    [Dependency] private readonly SharedStealthSystem _stealthSystem = null!;

    private bool StealthActive(EntityUid ent)
    {
        return CheckVisibility(ent);
    }
    private bool CheckVisibility(EntityUid ent)
    {
        if (!TryComp(ent, out StealthComponent? stealth))
            return false;

        var vis = _stealthSystem.GetVisibility(ent, stealth);

        var result = vis < stealth.ExamineThreshold;

        return result;
    }
}

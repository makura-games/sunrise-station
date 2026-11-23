using System.Diagnostics.CodeAnalysis;
using Content.Shared.Buckle.Components;
using Content.Shared.Humanoid;
using Content.Shared._Sunrise.CopyMachine;

namespace Content.Server._Sunrise.CopyMachine;

public sealed partial class CopyMachineSystem
{
    private void OnEntityStrapped(Entity<CopyMachineComponent> ent, ref StrappedEvent args) => QueueUIUpdate(ent);
    private void OnEntityUnstrapped(Entity<CopyMachineComponent> ent, ref UnstrappedEvent args) => QueueUIUpdate(ent);

    private bool TryGetBuckledHumanoidAppearance(Entity<CopyMachineComponent> ent, [NotNullWhen(true)] out HumanoidProfileComponent? humanoidAppearance)
    {
        humanoidAppearance = null;

        if (!TryComp<StrapComponent>(ent, out var strapComponent))
            return false;

        foreach (var buckledEntityUid in strapComponent.BuckledEntities)
        {
            if (TryComp(buckledEntityUid, out humanoidAppearance))
                return true;
        }

        return false;
    }
}

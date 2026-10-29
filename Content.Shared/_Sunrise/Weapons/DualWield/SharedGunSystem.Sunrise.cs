using System.Diagnostics.CodeAnalysis;
using Content.Shared._Sunrise.Weapons.DualWield;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

// File is intentionally placed in _Sunrise but extends the vanilla partial class.
#pragma warning disable IDE0130
namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    /// <summary>
    ///     In dual-wield mode, validates that the requested gun is one of the two registered
    ///     dual-wield guns. Outside dual-wield the active gun must match the requested gun.
    ///     Returns false when the request should be rejected.
    /// </summary>
    private bool TryHandleDualWieldShootRequest(
        EntityUid user,
        EntityUid activeGun,
        RequestShootEvent msg,
        out bool isDualWield,
        out DualWieldComponent? dualWield)
    {
        isDualWield = TryComp(user, out dualWield);

        var requestedGun = GetEntity(msg.Gun);

        if (isDualWield)
            return dualWield != null && (requestedGun == dualWield.LeftGun || requestedGun == dualWield.RightGun);

        return activeGun == requestedGun;
    }

    /// <summary>
    ///     After a shot attempt, rotates the dual-wield queue and delays the next gun so that
    ///     shots strictly alternate and never fire on the same tick.
    /// </summary>
    private void RotateDualWieldQueue(EntityUid user, GunComponent gun, bool isDualWield, DualWieldComponent? dualWield)
    {
        if (!isDualWield || dualWield == null || dualWield.GunQueue.Count <= 1)
            return;

        var front = dualWield.GunQueue[0];
        dualWield.GunQueue.RemoveAt(0);
        dualWield.GunQueue.Add(front);

        var nextGunUid = dualWield.GunQueue[0];
        if (gun.FireRateModified > 0f && TryComp<GunComponent>(nextGunUid, out var nextGun))
        {
            var halfInterval = TimeSpan.FromSeconds(0.5 / gun.FireRateModified);
            var earliest = Timing.CurTime + halfInterval;
            if (nextGun.NextFire < earliest)
            {
                nextGun.NextFire = earliest;
                DirtyField(nextGunUid, nextGun, nameof(GunComponent.NextFire));
            }
        }

        Dirty(user, dualWield);
    }

    /// <summary>
    ///     Stops shooting for both guns in a dual-wield pair, keeping their shot counters in sync.
    /// </summary>
    private void StopDualWieldShooting(EntityUid? user)
    {
        if (user == null)
            return;

        if (!TryComp<DualWieldComponent>(user.Value, out var dualWield))
            return;

        if (dualWield.LeftGun is { } leftUid && TryComp<GunComponent>(leftUid, out var leftGun))
            StopShooting(leftUid, leftGun);

        if (dualWield.RightGun is { } rightUid && TryComp<GunComponent>(rightUid, out var rightGun))
            StopShooting(rightUid, rightGun);
    }

    /// <summary>
    ///     Tries to get the front gun from the dual-wield queue, pruning any stale entries.
    ///     Returns true when a valid gun was found.
    /// </summary>
    private bool TryGetDualWieldGun(EntityUid entity, DualWieldComponent dualWield,
        out EntityUid gunEntity, [NotNullWhen(true)] out GunComponent? gunComp)
    {
        gunEntity = default;
        gunComp = null;

        var staleRemoved = false;

        for (var i = 0; i < dualWield.GunQueue.Count; i++)
        {
            if (TryComp<GunComponent>(dualWield.GunQueue[i], out var dwGunComp))
            {
                if (staleRemoved)
                    Dirty(entity, dualWield);

                gunEntity = dualWield.GunQueue[i];
                gunComp = dwGunComp;
                return true;
            }

            // Stale entity – remove and continue searching.
            dualWield.GunQueue.RemoveAt(i);
            staleRemoved = true;
            i--;
        }

        return false;
    }
}

using Content.Server._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Systems;

// Часть системы про иммунитет
public sealed partial class DiseaseSystem
{
    [Dependency] private EntityQuery<DiseaseImmunityComponent> _immunityQuery = default!;

    /// <summary>
    /// Проверяет, есть ли у сущности действующий иммунитет к болезни.
    /// </summary>
    public bool HasImmunity(EntityUid uid, EntityUid disease)
    {
        if (!_immunityQuery.TryComp(uid, out var immunity) ||
            !immunity.Immunities.TryGetValue(disease, out var expiresAt))
        {
            return false;
        }

        return expiresAt == null || expiresAt > _timing.CurTime;
    }

    /// <summary>
    /// Выдаёт иммунитет к болезни согласно политике иммунитета.
    /// </summary>
    public void GrantImmunity(EntityUid uid, EntityUid disease, DiseaseImmunityPolicy policy)
    {
        if (policy.Type == DiseaseImmunityType.None)
            return;

        var immunity = EnsureComp<DiseaseImmunityComponent>(uid);
        RemoveExpiredImmunities(immunity);

        immunity.Immunities[disease] = policy.Type == DiseaseImmunityType.Permanent
            ? null
            : _timing.CurTime + policy.Duration;
    }

    /// <summary>
    /// Снимает иммунитет к болезни.
    /// </summary>
    public void RemoveImmunity(EntityUid uid, EntityUid disease)
    {
        if (_immunityQuery.TryComp(uid, out var immunity))
            immunity.Immunities.Remove(disease);
    }

    private void RemoveExpiredImmunities(DiseaseImmunityComponent immunity)
    {
        var curTime = _timing.CurTime;

        foreach (var (disease, expiresAt) in immunity.Immunities)
        {
            if (expiresAt != null && expiresAt <= curTime)
                immunity.Immunities.Remove(disease);
        }
    }
}

using Content.Server._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Systems;

public sealed partial class DiseaseSystem
{
    /*
     * Часть системы про иммунитет: проверка иммунитета и его выдача после выздоровления.
     */

    [Dependency] private EntityQuery<DiseaseImmunityComponent> _immunityQuery = default!;

    private readonly List<EntityUid> _expiredImmunities = new();

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

        _expiredImmunities.Clear();
        foreach (var (disease, expiresAt) in immunity.Immunities)
        {
            if (expiresAt != null && expiresAt <= curTime)
                _expiredImmunities.Add(disease);
        }

        foreach (var disease in _expiredImmunities)
        {
            immunity.Immunities.Remove(disease);
        }
    }
}

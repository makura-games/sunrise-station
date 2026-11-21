using Content.Server._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Components;

/// <summary>
/// Иммунитеты, полученные после выздоровления от болезней.
/// </summary>
[RegisterComponent, Access(typeof(DiseaseSystem))]
public sealed partial class DiseaseImmunityComponent : Component
{
    /// <summary>
    /// Срок действия каждого иммунитета. Null — иммунитет держится до конца раунда.
    /// Просроченные записи удаляются при следующей проверке.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan?> Immunities = new();
}

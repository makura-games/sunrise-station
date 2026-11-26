namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Иммунитет, который носитель получает после выздоровления — от лечения или само по себе.
/// </summary>
[DataDefinition]
public sealed partial class DiseaseImmunityPolicy
{
    /// <summary>
    /// Какой иммунитет выдавать.
    /// </summary>
    [DataField]
    public DiseaseImmunityType Type = DiseaseImmunityType.None;

    /// <summary>
    /// Срок иммунитета. Учитывается только при <see cref="DiseaseImmunityType.Temporary"/>.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(10);
}

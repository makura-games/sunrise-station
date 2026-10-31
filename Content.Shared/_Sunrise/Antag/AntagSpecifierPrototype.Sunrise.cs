using Robust.Shared.Prototypes;
using AntagPrototype = Content.Shared.Roles.AntagPrototype;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Antag;

public sealed partial class AntagSpecifierPrototype
{
    /// <summary>
    /// Резервные предпочтения для командных антагонистов, если основное предпочтение не выбрано.
    /// </summary>
    [DataField]
    public List<ProtoId<AntagPrototype>> FallbackRoles = new();

    /// <summary>
    /// Максимальное число выбранных представителей командования. Ноль снимает ограничение.
    /// </summary>
    [DataField]
    public int MaxCommandStaff;

    /// <summary>
    /// Разрешает выбирать представителей командования для этого антагониста.
    /// </summary>
    [DataField]
    public bool PickCommandStaff;

    /// <summary>
    /// Игнорирует ограничения должностей для специальных событий.
    /// </summary>
    [DataField]
    public bool IgnoreJobRestrictions;
}

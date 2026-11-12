using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.SponsorInventory;

/// <summary>
/// Общий признак предметов, выдаваемых исключительно через спонсорский инвентарь.
/// </summary>
public static class SponsorExclusive
{
    /// <summary>
    /// Категория общая для проектов и наследуется вариантами помеченных предметов.
    /// </summary>
    public static readonly ProtoId<EntityCategoryPrototype> Category = "SponsorExclusive";

    /// <summary>
    /// Проверяет итоговые категории прототипа после обработки наследования движком.
    /// </summary>
    public static bool IsExclusive(EntityPrototype prototype)
    {
        foreach (var category in prototype.Categories)
        {
            if (category.ID == Category.Id)
                return true;
        }

        return false;
    }
}

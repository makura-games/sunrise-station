using Robust.Shared.Prototypes;

namespace Content.Shared.Metabolism;

public sealed partial class MetabolizerSystem
{
    /// <summary>
    /// Добавляет органу тип метаболизатора.
    /// </summary>
    public bool TryAddMetabolizerType(
        Entity<MetabolizerComponent> ent,
        ProtoId<MetabolizerTypePrototype> metabolizerType)
    {
        ent.Comp.MetabolizerTypes ??= [];
        if (!ent.Comp.MetabolizerTypes.Add(metabolizerType))
            return false;

        Dirty(ent);
        return true;
    }

    /// <summary>
    /// Удаляет у органа тип метаболизатора.
    /// </summary>
    public bool TryRemoveMetabolizerType(
        Entity<MetabolizerComponent> ent,
        ProtoId<MetabolizerTypePrototype> metabolizerType)
    {
        if (ent.Comp.MetabolizerTypes is not { } metabolizerTypes ||
            !metabolizerTypes.Remove(metabolizerType))
        {
            return false;
        }

        Dirty(ent);
        return true;
    }
}

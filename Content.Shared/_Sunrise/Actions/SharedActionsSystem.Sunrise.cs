using Content.Shared.Actions.Components;

namespace Content.Shared.Actions;

public abstract partial class SharedActionsSystem
{
    /// <summary>
    /// Изменяет максимальную дальность выбора цели для action.
    /// </summary>
    public void SetRange(Entity<TargetActionComponent?> ent, float value)
    {
        if (!_targetActionQuery.Resolve(ent, ref ent.Comp) || ent.Comp.Range.Equals(value))
            return;

        ent.Comp.Range = value;
        DirtyField(ent, ent.Comp, nameof(TargetActionComponent.Range));
    }
}

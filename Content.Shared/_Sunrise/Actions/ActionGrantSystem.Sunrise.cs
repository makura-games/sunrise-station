using Robust.Shared.Prototypes;

namespace Content.Shared.Actions;

public sealed partial class ActionGrantSystem
{
    // Добавление и удаление наборов действий у уже созданных сущностей. Систему стереть в пыль, когда оффы добавят. Невероятно странно что у них все еще нет таких методов.

    /// <summary>
    /// Добавляет действия в набор выдачи. После MapInit сразу выдаёт недостающие действия.
    /// </summary>
    public void AddActions(Entity<ActionGrantComponent?> ent, IReadOnlyList<EntProtoId> prototypes)
    {
        ent.Comp ??= EnsureComp<ActionGrantComponent>(ent);

        foreach (var prototype in prototypes)
        {
            if (!ent.Comp.Actions.Contains(prototype))
                ent.Comp.Actions.Add(prototype);

            if (LifeStage(ent) < EntityLifeStage.MapInitialized)
                continue;

            EntityUid? actionEntity = null;
            foreach (var action in _actions.GetActions(ent))
            {
                if (action.Comp.Container != ent.Owner || Prototype(action)?.ID != prototype.Id)
                    continue;

                actionEntity = action;
                break;
            }

            actionEntity ??= _actions.AddAction(ent, prototype, ent);
            if (actionEntity is { } granted && !ent.Comp.ActionEntities.Contains(granted))
                ent.Comp.ActionEntities.Add(granted);
        }

        Dirty(ent);
    }

    /// <summary>
    /// Убирает указанные действия из набора выдачи, сохраняя остальные действия сущности.
    /// </summary>
    public void RemoveActions(Entity<ActionGrantComponent?> ent, IReadOnlyList<EntProtoId> prototypes)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        foreach (var prototype in prototypes)
        {
            ent.Comp.Actions.RemoveAll(action => action == prototype);

            for (var i = ent.Comp.ActionEntities.Count - 1; i >= 0; i--)
            {
                var actionEntity = ent.Comp.ActionEntities[i];
                if (_actions.GetAction(actionEntity) is not { } action)
                {
                    ent.Comp.ActionEntities.RemoveAt(i);
                    continue;
                }

                if (Prototype(action)?.ID != prototype.Id)
                    continue;

                if (action.Comp.AttachedEntity == ent.Owner)
                    _actions.RemoveAction(action.AsNullable());

                ent.Comp.ActionEntities.RemoveAt(i);
            }
        }

        Dirty(ent);
    }
}

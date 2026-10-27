using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;
using Content.Shared._Sunrise.Misc;
using System.Collections.Generic;

namespace Content.Shared.Xenoarchaeology.Artifact;

public abstract partial class SharedXenoArtifactSystem
{
    public void RemoveXenoArtifactComponent(EntityUid target)
    {
        if (HasComp<XenoArtifactThrowingAutoInjectorMarkComponent>(target))
            RemComp<XenoArtifactThrowingAutoInjectorMarkComponent>(target);
        if (HasComp<XenoArtifactComponent>(target))
            RemCompDeferred<XenoArtifactComponent>(target);
    }

    private void OnShutdown(Entity<XenoArtifactComponent> ent, ref ComponentShutdown args)
    {
        // Удаляем action
        if (TryComp<ActionsComponent>(ent.Owner, out var actionsComp))
        {
            foreach (var actionId in actionsComp.Actions)
            {
                if (_actions.GetAction(actionId) is { } actionEnt &&
                    TryComp(actionEnt.Owner, out MetaDataComponent? meta) &&
                    meta.EntityPrototype?.ID != null &&
                    meta.EntityPrototype.ID == ent.Comp.SelfActivateAction)
                {
                    _actions.RemoveAction(new Entity<ActionsComponent?>(ent.Owner, actionsComp), actionId);
                    break;
                }
            }
        }
        // Удаляем все nodes/эффекты из NodeContainer
        if (ent.Comp.NodeContainer != null)
        {
            var toDelete = new List<EntityUid>(ent.Comp.NodeContainer.ContainedEntities);
            foreach (var node in toDelete)
            {
                QueueDel(node);
            }
        }
    }
}

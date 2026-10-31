using Robust.Shared.Containers;
using Robust.Shared.Map;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    public bool TryGetOrganWithComponent<TComp>(Entity<BodyComponent?> body, out Entity<TComp> organ)
        where TComp : Component
    {
        organ = default;
        if (!TryGetOrgansContainer(body, out var organs))
            return false;

        foreach (var organUid in organs.ContainedEntities)
        {
            if (!TryComp<TComp>(organUid, out var component))
                continue;

            organ = (organUid, component);
            return true;
        }

        return false;
    }

    public bool CanInsertOrgan(Entity<BodyComponent?> body, Entity<OrganComponent?> organ)
    {
        return TryGetOrgansContainer(body, out var organs) &&
               _organQuery.Resolve(organ.Owner, ref organ.Comp, logMissing: false) &&
               _container.CanInsert(organ.Owner, organs);
    }

    public bool TryInsertOrgan(Entity<BodyComponent?> body, Entity<OrganComponent?> organ, bool force = false)
    {
        if (!TryGetOrgansContainer(body, out var organs) ||
            !_organQuery.Resolve(organ.Owner, ref organ.Comp, logMissing: false))
        {
            return false;
        }

        if (!force && !CanInsertOrgan(body, organ))
            return false;

        return _container.Insert((organ.Owner, null, null, null), organs, force: force);
    }

    public bool CanRemoveOrgan(Entity<OrganComponent?> organ)
    {
        if (!_organQuery.Resolve(organ.Owner, ref organ.Comp, logMissing: false) ||
            organ.Comp.Body is not { } bodyUid)
        {
            return false;
        }

        return CanRemoveOrgan((bodyUid, null), organ);
    }

    public bool CanRemoveOrgan(Entity<BodyComponent?> body, Entity<OrganComponent?> organ)
    {
        return TryGetOrgansContainer(body, out var organs) &&
               _organQuery.Resolve(organ.Owner, ref organ.Comp, logMissing: false) &&
               organs.Contains(organ.Owner) &&
               _container.CanRemove(organ.Owner, organs);
    }

    public bool TryRemoveOrgan(
        Entity<OrganComponent?> organ,
        bool reparent = true,
        bool force = false,
        EntityCoordinates? destination = null)
    {
        if (!_organQuery.Resolve(organ.Owner, ref organ.Comp, logMissing: false) ||
            organ.Comp.Body is not { } bodyUid)
        {
            return false;
        }

        return TryRemoveOrgan((bodyUid, null), organ, reparent, force, destination);
    }

    public bool TryRemoveOrgan(
        Entity<BodyComponent?> body,
        Entity<OrganComponent?> organ,
        bool reparent = true,
        bool force = false,
        EntityCoordinates? destination = null)
    {
        if (!TryGetOrgansContainer(body, out var organs) ||
            !_organQuery.Resolve(organ.Owner, ref organ.Comp, logMissing: false) ||
            !organs.Contains(organ.Owner))
        {
            return false;
        }

        if (!force && !CanRemoveOrgan(body, organ))
            return false;

        return _container.Remove((organ.Owner, null, null), organs, reparent: reparent, force: force, destination: destination);
    }

    private bool TryGetOrgansContainer(Entity<BodyComponent?> body, out Container organs)
    {
        organs = default!;
        if (!_bodyQuery.Resolve(body.Owner, ref body.Comp, logMissing: false) ||
            body.Comp.Organs is not { } bodyOrgans)
        {
            return false;
        }

        organs = bodyOrgans;
        return true;
    }
}

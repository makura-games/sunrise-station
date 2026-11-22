using Content.Shared.Access.Components;
using Content.Shared.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Sandbox.Access.Systems;

/// <summary>
/// Resolves which access reader data should be displayed for mapping access overlays.
/// </summary>
public sealed class MappingAccessReaderResolver : IDisposable
{
    private readonly IEntityManager _ent;
    private readonly EntityQuery<ContainerFillComponent> _containerFillQuery;
    private readonly SharedContainerSystem _containerSystem;
    private readonly IPrototypeManager _prototypeManager;

    private readonly Dictionary<EntityUid, AccessReaderComponent> _accessReaderLookup = new();
    private readonly Dictionary<string, AccessReaderComponent?> _prototypeAccessReaderLookup = new();

    private bool _accessReaderLookupDirty = true;

    public MappingAccessReaderResolver(
        IEntityManager entityManager,
        IPrototypeManager prototypeManager,
        EntityQuery<ContainerFillComponent> containerFillQuery)
    {
        _ent = entityManager;
        _containerFillQuery = containerFillQuery;
        _containerSystem = _ent.System<SharedContainerSystem>();
        _prototypeManager = prototypeManager;
        _prototypeManager.PrototypesReloaded += OnPrototypesReloaded;
    }

    public void Dispose()
    {
        _prototypeManager.PrototypesReloaded -= OnPrototypesReloaded;
    }

    public void MarkAccessReaderLookupDirty()
    {
        _accessReaderLookupDirty = true;
    }

    public void SyncAccessReaderLookup(EntityUid uid, AccessReaderComponent accessReader)
    {
        if (!accessReader.Enabled || accessReader.AccessLists.Count == 0)
        {
            _accessReaderLookup.Remove(uid);
            return;
        }

        _accessReaderLookup[uid] = accessReader;
    }

    public void RemoveAccessReaderLookup(EntityUid uid)
    {
        _accessReaderLookup.Remove(uid);
    }

    public bool TryGetDisplayedAccessReader(
        EntityUid uid,
        AccessReaderComponent accessReader,
        bool electronicsOnly,
        out AccessReaderComponent displayedReader)
    {
        if (!electronicsOnly)
        {
            displayedReader = accessReader;
            return accessReader.Enabled && accessReader.AccessLists.Count > 0;
        }

        if (_accessReaderLookupDirty)
            RebuildAccessReaderLookup();

        return TryGetElectronicsAccessReader(uid, accessReader, out displayedReader);
    }

    private bool TryGetElectronicsAccessReader(
        EntityUid uid,
        AccessReaderComponent accessReader,
        out AccessReaderComponent electronicsReader)
    {
        electronicsReader = default!;

        if (accessReader.ContainerAccessProvider is not { } containerId)
            return false;

        if (TryGetContainedAccessReader(uid, containerId, out electronicsReader))
            return true;

        return TryGetPrototypeElectronicsAccessReader(uid, containerId, out electronicsReader);
    }

    private bool TryGetContainedAccessReader(
        EntityUid uid,
        string containerId,
        out AccessReaderComponent electronicsReader)
    {
        electronicsReader = default!;

        if (!_containerSystem.TryGetContainer(uid, containerId, out var container))
            return false;

        var foundReader = false;
        var selectedUid = EntityUid.Invalid;

        foreach (var containedUid in container.ContainedEntities)
        {
            if (!_accessReaderLookup.TryGetValue(containedUid, out var containedReader))
                continue;

            if (foundReader && containedUid.Id >= selectedUid.Id)
                continue;

            foundReader = true;
            selectedUid = containedUid;
            electronicsReader = containedReader;
        }

        return foundReader;
    }

    private bool TryGetPrototypeElectronicsAccessReader(
        EntityUid uid,
        string containerId,
        out AccessReaderComponent electronicsReader)
    {
        electronicsReader = default!;

        if (!_containerFillQuery.TryComp(uid, out var containerFill) ||
            !containerFill.Containers.TryGetValue(containerId, out var prototypes))
        {
            return false;
        }

        foreach (var prototypeId in prototypes)
        {
            if (!TryGetPrototypeAccessReader(prototypeId, out electronicsReader))
                continue;

            return true;
        }

        return false;
    }

    private bool TryGetPrototypeAccessReader(string prototypeId, out AccessReaderComponent accessReader)
    {
        accessReader = default!;

        if (_prototypeAccessReaderLookup.TryGetValue(prototypeId, out var cachedReader))
        {
            if (cachedReader == null)
                return false;

            accessReader = cachedReader;
            return true;
        }

        if (!_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var prototype) ||
            !prototype.TryGetComponent<AccessReaderComponent>(out var prototypeReader, _ent.ComponentFactory) ||
            !prototypeReader.Enabled ||
            prototypeReader.AccessLists.Count == 0)
        {
            _prototypeAccessReaderLookup[prototypeId] = null;
            return false;
        }

        _prototypeAccessReaderLookup[prototypeId] = prototypeReader;
        accessReader = prototypeReader;
        return true;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        _prototypeAccessReaderLookup.Clear();
    }

    private void RebuildAccessReaderLookup()
    {
        _accessReaderLookup.Clear();

        var query = _ent.AllEntityQueryEnumerator<AccessReaderComponent>();
        while (query.MoveNext(out var uid, out var accessReader))
        {
            if (!accessReader.Enabled || accessReader.AccessLists.Count == 0)
                continue;

            _accessReaderLookup[uid] = accessReader;
        }

        _accessReaderLookupDirty = false;
    }
}

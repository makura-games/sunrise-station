using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Research.Artifact.Effects.RandomTransformation;

public sealed partial class ArtifactRandomTransformationSystem
{
    /*
     * Candidate caching and prototype filtering part of the system.
     */

    private void OnStartup(Entity<ArtifactRandomTransformationComponent> ent, ref ComponentStartup args)
    {
        TryGetTransformCandidates(ent, out _);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        RebuildCandidateCaches();
    }

    /// <summary>
    /// Rebuilds the shared prefiltered prototype pool and clears all node-specific candidate caches.
    /// </summary>
    private void RebuildCandidateCaches()
    {
        _candidateCache.Clear();
        _baseCandidatePool.Clear();

        foreach (var prototype in ProtoMan.EnumeratePrototypes<EntityPrototype>())
        {
            if (!CanEverTransformInto(prototype))
                continue;

            _baseCandidatePool.Add(prototype);
        }
    }

    /// <summary>
    /// Applies node-independent checks that are safe to evaluate once per prototype reload.
    /// </summary>
    private static bool CanEverTransformInto(EntityPrototype proto)
    {
        if (proto.Abstract)
            return false;

        if (!proto.MapSavable)
            return false;

        return true;
    }

    /// <summary>
    /// Returns the cached transformation pool for the given node, rebuilding it on first use.
    /// </summary>
    private bool TryGetTransformCandidates(
        Entity<ArtifactRandomTransformationComponent> ent,
        out IReadOnlyList<EntityPrototype> candidates)
    {
        var prototypeId = MetaData(ent).EntityPrototype?.ID;
        if (prototypeId == null)
        {
            var uncachedCandidates = BuildTransformCandidates(ent.Comp);
            candidates = uncachedCandidates;
            return uncachedCandidates.Count > 0;
        }

        if (!_candidateCache.TryGetValue(prototypeId, out var cachedCandidates))
        {
            cachedCandidates = BuildTransformCandidates(ent.Comp);
            _candidateCache[prototypeId] = cachedCandidates;
        }

        candidates = cachedCandidates;
        return cachedCandidates.Count > 0;
    }

    /// <summary>
    /// Checks whether the specified prototype is allowed for this node's transformation pool.
    /// </summary>
    /// <remarks>
    /// This is kept public so tests and diagnostics can validate the filter without triggering the effect.
    /// </remarks>
    public bool CanTransformInto(Entity<ArtifactRandomTransformationComponent> ent, EntityPrototype proto)
    {
        return CanTransformInto(ent.Comp, proto);
    }

    private List<EntityPrototype> BuildTransformCandidates(ArtifactRandomTransformationComponent component)
    {
        var candidates = new List<EntityPrototype>();

        // Build node-specific candidates from the shared prefiltered pool instead of all entity prototypes.
        foreach (var prototype in _baseCandidatePool)
        {
            if (!CanTransformInto(component, prototype))
                continue;

            candidates.Add(prototype);
        }

        return candidates;
    }

    private bool CanTransformInto(ArtifactRandomTransformationComponent component, EntityPrototype proto)
    {
        if (!CanEverTransformInto(proto))
            return false;

        if (component.RequiredComponents != null &&
            MissingRequiredComponent(component.RequiredComponents, proto))
            return false;

        if (component.PrototypeBlacklist != null && component.PrototypeBlacklist.Contains(proto.ID))
            return false;

        var isException = component.PrototypeBlacklistExceptions != null &&
                          component.PrototypeBlacklistExceptions.Contains(proto.ID);

        if (!isException &&
            component.PrototypeBlacklist != null &&
            HasBlacklistedParent(proto.ID, component.PrototypeBlacklist))
            return false;

        if (component.ComponentBlacklist != null &&
            HasBlacklistedComponent(proto, component.ComponentBlacklist))
            return false;

        if (component.CategoryBlacklist != null &&
            HasBlacklistedCategory(proto, component.CategoryBlacklist))
            return false;

        if (ContainsBlacklistedSubstring(proto.ID, component.PrototypeIdBlacklistSubstrings))
            return false;

        if (ContainsBlacklistedSubstring(proto.SetSuffix, component.PrototypeSuffixBlacklistSubstrings))
            return false;

        return true;
    }

    private bool MissingRequiredComponent(HashSet<string> requiredComponents, EntityPrototype proto)
    {
        foreach (var required in requiredComponents)
        {
            if (!proto.Components.ContainsKey(required))
                return true;
        }

        return false;
    }

    private bool HasBlacklistedParent(EntProtoId prototypeId, HashSet<EntProtoId> prototypeBlacklist)
    {
        foreach (var parent in ProtoMan.EnumerateAllParents<EntityPrototype>(prototypeId))
        {
            if (prototypeBlacklist.Contains(parent.id))
                return true;
        }

        return false;
    }

    private static bool HasBlacklistedComponent(EntityPrototype proto, HashSet<string> componentBlacklist)
    {
        foreach (var componentId in proto.Components.Keys)
        {
            if (componentBlacklist.Contains(componentId))
                return true;
        }

        return false;
    }

    private static bool HasBlacklistedCategory(
        EntityPrototype proto,
        HashSet<ProtoId<EntityCategoryPrototype>> categoryBlacklist)
    {
        foreach (var category in proto.Categories)
        {
            if (categoryBlacklist.Contains(category.ID))
                return true;
        }

        return false;
    }

    private static bool ContainsBlacklistedSubstring(string? value, IReadOnlyCollection<string>? blacklist)
    {
        if (string.IsNullOrWhiteSpace(value) || blacklist == null)
            return false;

        foreach (var substring in blacklist)
        {
            if (value.Contains(substring, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

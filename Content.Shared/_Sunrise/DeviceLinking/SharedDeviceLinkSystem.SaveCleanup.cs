#pragma warning disable IDE0130
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeviceLinking;

public abstract partial class SharedDeviceLinkSystem
{
    [Dependency] private EntityQuery<TransformComponent> _saveCleanupXformQuery = default!;

    /*
     * Save-cleanup helpers for device links before map serialization.
     */
    /// <summary>
    /// Removes invalid or unsaveable device-link references from a map before map serialization.
    /// </summary>
    /// <param name="mapId">The single map that is about to be serialized.</param>
    public DeviceLinkSaveCleanupResult CleanupLinksForMapSave(MapId mapId)
    {
        return CleanupLinksForMapSave(new HashSet<MapId> { mapId });
    }

    /// <summary>
    /// Removes invalid or unsaveable device-link references from the provided saved map set.
    /// </summary>
    /// <param name="mapIds">The maps included in the pending save operation.</param>
    public DeviceLinkSaveCleanupResult CleanupLinksForMapSave(HashSet<MapId> mapIds)
    {
        var result = new DeviceLinkSaveCleanupResult();
        var invalidLinks = new List<(ProtoId<SourcePortPrototype> Source, ProtoId<SinkPortPrototype> Sink)>();
        var sinksToRemove = new List<(EntityUid SinkUid, DeviceLinkSinkComponent? SinkComponent, string Reason)>();
        var sourceEnumerator = EntityQueryEnumerator<DeviceLinkSourceComponent, TransformComponent>();
        while (sourceEnumerator.MoveNext(out var sourceUid, out var sourceComponent, out var sourceXform))
        {
            if (!mapIds.Contains(sourceXform.MapID))
                continue;

            var changed = false;
            sinksToRemove.Clear();

            foreach (var (sinkUid, links) in sourceComponent.LinkedPorts)
            {
                if (!_deviceLinkSinkQuery.TryComp(sinkUid, out var sinkComponent))
                {
                    sinksToRemove.Add((sinkUid, null, "missing sink component or entity"));
                    result = result with { RemovedSinkEntries = result.RemovedSinkEntries + 1 };
                    continue;
                }

                if (!_saveCleanupXformQuery.TryComp(sinkUid, out var sinkXform) || !mapIds.Contains(sinkXform.MapID))
                {
                    sinksToRemove.Add((sinkUid, sinkComponent, "sink is not on the saved map set"));
                    result = result with { RemovedSinkEntries = result.RemovedSinkEntries + 1 };
                    continue;
                }

                foreach (var link in links)
                {
                    if (sourceComponent.Ports.Contains(link.Source) && sinkComponent.Ports.Contains(link.Sink))
                        continue;

                    invalidLinks.Add(link);
                }

                if (invalidLinks.Count == 0)
                    continue;

                foreach (var link in invalidLinks)
                {
                    links.Remove(link);
                    result = result with { RemovedLinkPairs = result.RemovedLinkPairs + 1 };
                    Log.Warning(
                        $"Device source {ToPrettyString(sourceUid)} contains invalid save link to {ToPrettyString(sinkUid)}: {link.Source}->{link.Sink}. Removing link.");
                }

                changed = true;
                invalidLinks.Clear();

                if (links.Count == 0)
                {
                    sinksToRemove.Add((sinkUid, sinkComponent, "no valid port pairs remain"));
                    result = result with { RemovedSinkEntries = result.RemovedSinkEntries + 1 };
                }
            }

            if (!changed && sinksToRemove.Count == 0)
                continue;

            foreach (var (sinkUid, sinkComponent, reason) in sinksToRemove)
            {
                sourceComponent.LinkedPorts.Remove(sinkUid);
                sinkComponent?.LinkedSources.Remove(sourceUid);

                Log.Warning(
                    $"Device source {ToPrettyString(sourceUid)} contains invalid save sink {ToPrettyString(sinkUid)}: {reason}. Removing sink reference.");

                if (sinkComponent is not null)
                    Dirty(sinkUid, sinkComponent);
            }

            RebuildOutputs(sourceComponent);
            Dirty(sourceUid, sourceComponent);
            result = result with { AffectedSources = result.AffectedSources + 1 };
        }

        return result;
    }

    private static void RebuildOutputs(DeviceLinkSourceComponent sourceComponent)
    {
        sourceComponent.Outputs.Clear();

        foreach (var (sinkUid, links) in sourceComponent.LinkedPorts)
        {
            foreach (var (sourcePort, _) in links)
            {
                sourceComponent.Outputs.GetOrNew(sourcePort).Add(sinkUid);
            }
        }
    }
}

/// <summary>
/// Describes how many saved device-link references were removed during cleanup.
/// </summary>
/// <param name="AffectedSources">How many source entities needed any cleanup.</param>
/// <param name="RemovedSinkEntries">How many sink references were removed from source link tables.</param>
/// <param name="RemovedLinkPairs">How many invalid source-port to sink-port pairs were removed.</param>
public readonly record struct DeviceLinkSaveCleanupResult(
    int AffectedSources,
    int RemovedSinkEntries,
    int RemovedLinkPairs);

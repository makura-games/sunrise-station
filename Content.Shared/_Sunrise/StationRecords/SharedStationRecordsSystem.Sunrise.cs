using System.Diagnostics.CodeAnalysis;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.StationRecords;

public abstract partial class SharedStationRecordsSystem
{
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Возвращает случайную запись, исключая записи с указанными идентификаторами.
    /// </summary>
    public bool TryGetRandomRecord<T>(
        Entity<StationRecordsComponent?> ent,
        [NotNullWhen(true)] out T? entry,
        HashSet<uint> ignoredIds,
        EntityUid? seedEntity = null)
    {
        entry = default;

        if (!Resolve(ent.Owner, ref ent.Comp))
            return false;

        var filtered = new List<uint>();
        foreach (var id in ent.Comp.Records.Keys)
        {
            if (!ignoredIds.Contains(id))
                filtered.Add(id);
        }

        if (filtered.Count == 0)
            return false;

        var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(seedEntity ?? ent.Owner));
        var key = random.Pick(filtered);
        return ent.Comp.Records.TryGetRecordEntry(key, out entry);
    }
}

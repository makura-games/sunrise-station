using System.Diagnostics.CodeAnalysis;
using Content.Shared.Preferences;
using Content.Shared.Random.Helpers;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationRecords.Components;
using Robust.Shared.Random;

namespace Content.Shared.StationRecords.Systems;

public sealed partial class StationRecordsSystem
{
    private void GetSunriseRecordIdentity(
        EntityUid player,
        HumanoidCharacterProfile profile,
        ref EntityUid? idUid,
        out string name,
        out bool silicon)
    {
        name = profile.Name;
        if (idUid == null)
        {
            idUid = player;
            name = MetaData(player).EntityName;
        }

        silicon = HasComp<BorgChassisComponent>(player) || HasComp<StationAiHeldComponent>(player);
    }

    /// <summary>
    /// Возвращает случайную запись, исключая записи с указанными идентификаторами.
    /// </summary>
    public bool TryGetRandomRecord<T>(
        Entity<StationRecordsComponent?> ent,
        [NotNullWhen(true)] out T? entry,
        HashSet<uint> ignoredIds,
        EntityUid? seedEntity = null) where T : StationRecord
    {
        entry = default;

        if (!_recordsQuery.Resolve(ent.Owner, ref ent.Comp))
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

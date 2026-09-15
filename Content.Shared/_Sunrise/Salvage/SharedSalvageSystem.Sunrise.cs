using System;
using System.Linq;
using Content.Shared.Salvage.Expeditions;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Salvage;

public abstract partial class SharedSalvageSystem
{
    public SalvageFactionPrototype GetFactionPrototype(
        List<ProtoId<SalvageFactionPrototype>>? dungeonFactions,
        string difficultyId,
        RobustRandom rand)
    {
        var factionProtos = _proto.EnumeratePrototypes<SalvageFactionPrototype>().ToList();

        if (dungeonFactions != null && dungeonFactions.Count > 0)
            factionProtos = dungeonFactions.ConvertAll(new Converter<ProtoId<SalvageFactionPrototype>, SalvageFactionPrototype>(_proto.Index));

        var byDifficulty = factionProtos
            .Where(x => x.Difficulties == null || x.Difficulties.Contains(difficultyId))
            .ToList();

        if (byDifficulty.Count > 0)
            factionProtos = byDifficulty;

        factionProtos.Sort((x, y) => string.Compare(x.ID, y.ID, StringComparison.Ordinal));
        return factionProtos[rand.Next(factionProtos.Count)];
    }
}

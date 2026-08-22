using Content.Server._Sunrise.Silicons.Laws.Components;
using Content.Server._Sunrise.StationEvents.Components;
using Content.Server.Silicons.Laws;
using Content.Server.StationEvents.Events;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.StationEvents.Events;

/// <summary>
/// Game rule for changing borg laws to Epsilon during Epsilon alert level.
/// </summary>
public sealed partial class EpsilonDeathSquadLawsetRule : StationEventSystem<EpsilonDeathSquadLawsetComponent>
{
    private EntityUid? _targetStation;
    [Dependency] private SiliconLawSystem _siliconLaw = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private const string DeathSquadLawsetId = "DeathSquadLawset";

    public void StartEvent(EntityUid ruleEntity, EntityUid station)
    {
        _targetStation = station;
        RunEpsilonLawset(ruleEntity);
    }

    private void RunEpsilonLawset(EntityUid ruleEntity)
    {
        if (_targetStation == null)
        {
            Sawmill.Error($"Target station not set for EpsilonDeathSquadLawsetRule");
            return;
        }

        var lawsetId = DeathSquadLawsetId;
        if (!_prototypeManager.TryIndex<SiliconLawsetPrototype>(lawsetId, out var lawsetProto))
        {
            Sawmill.Error($"Could not find lawset prototype: {lawsetId}");
            return;
        }

        var laws = new List<SiliconLaw>();
        foreach (var lawId in lawsetProto.Laws)
        {
            if (!_prototypeManager.TryIndex<SiliconLawPrototype>(lawId, out var lawProto))
            {
                Sawmill.Error($"Could not find law prototype: {lawId}");
                return;
            }

            laws.Add(new SiliconLaw
            {
                LawString = Loc.GetString(lawProto.LawString),
                Order = lawProto.Order
            });
        }


        var borgCount = 0;
        var changedCount = 0;
        var query = EntityQueryEnumerator<SiliconLawProviderComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var provider, out var xform))
        {
            borgCount++;
            var borgGrid = xform.GridUid;

            if (HasComp<BlockLawChangeComponent>(ent))
            {
                continue;
            }

            // Only change laws for borgs on grids that belong to the chosen station
            if (borgGrid == null || !TryComp<StationDataComponent>(_targetStation.Value, out var stationData))
            {
                continue;
            }

            var stationGrids = stationData.Grids as IReadOnlySet<EntityUid>;
            if (stationGrids == null || !stationGrids.Contains(borgGrid.Value))
            {
                continue;
            }

            _siliconLaw.SetLaws(laws, ent, provider.LawUploadSound);
            changedCount++;
        }
    }
}

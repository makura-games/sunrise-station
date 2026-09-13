using Content.Shared._Sunrise.Laws;
using Content.Shared._Sunrise.Laws.Components;
using Content.Shared._Sunrise.Laws.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;


namespace Content.Server._Sunrise.Laws.Systems;

public sealed partial class StationCorporateLawSystem : SharedStationCorporateLawSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationCorporateLawComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, StationCorporateLawComponent component, MapInitEvent args)
    {
        InitializeLawset(uid);
    }

    private void InitializeLawset(EntityUid station)
    {
        var lawsetId = _cfg.GetCVar(SunriseCCVars.CorporateLawSet);
        if (!_proto.TryIndex<CorporateLawsetPrototype>(lawsetId, out var prototype))
            return;

        if (!TryComp<StationCorporateLawComponent>(station, out var component))
            return;

        // Sunrise-Edit: Check if already initialized to avoid marking as modified in PrototypeSaveTest
        if (component.LawsetPrototype == lawsetId)
            return;

        component.Provisions = new List<ProtoId<CorporateLawPrototype>>(prototype.Provisions);
        component.Circumstances = new List<ProtoId<CorporateLawPrototype>>(prototype.Circumstances);
        component.Articles = new List<ProtoId<CorporateLawSectionPrototype>>(prototype.Articles);
        component.PermanentSentenceThreshold = prototype.PermanentSentenceThreshold;
        component.LawsetPrototype = lawsetId;

        Dirty(station, component);
    }
}

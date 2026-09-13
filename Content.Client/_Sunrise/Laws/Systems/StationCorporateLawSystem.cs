using Content.Shared._Sunrise.Laws;
using Content.Shared._Sunrise.Laws.Components;
using Content.Shared._Sunrise.Laws.Systems;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;


namespace Content.Client._Sunrise.Laws.Systems;

public sealed partial class StationCorporateLawSystem : SharedStationCorporateLawSystem
{
    [Dependency] private Robust.Shared.Configuration.IConfigurationManager _config = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationCorporateLawComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<StationCorporateLawComponent> ent, ref ComponentStartup args)
    {
        // If we are on a station and have no articles/provisions, try to initialize from CVar.
        // This acts as a prediction/fallback until the server's state syncs.
        var component = ent.Comp;
        if (component.Articles.Count > 0 || component.Provisions.Count > 0)
            return;

        if (!HasComp<StationDataComponent>(ent.Owner))
            return;

        var lawsetId = _config.GetCVar(SunriseCCVars.CorporateLawSet);
        if (!_proto.TryIndex<CorporateLawsetPrototype>(lawsetId, out var prototype))
            return;

        component.Provisions = new(prototype.Provisions);
        component.Articles = new(prototype.Articles);
        component.Circumstances = new(prototype.Circumstances);
        component.PermanentSentenceThreshold = prototype.PermanentSentenceThreshold;
    }
}

using System.Linq;
using Content.Shared._Sunrise.Laws.Components;
using Content.Shared.Station;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;


namespace Content.Shared._Sunrise.Laws.Systems;

public abstract partial class SharedStationCorporateLawSystem : EntitySystem
{
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IConfigurationManager _config = default!;

    public Entity<StationCorporateLawComponent>? GetStationLawset(EntityUid uid)
    {
        var station = _station.GetOwningStation(uid);
        if (station == null || !TryComp<StationCorporateLawComponent>(station, out var component))
            return null;

        return (station.Value, component);
    }

    /// <summary>
    ///     Gets the laws from the station component.
    ///     If the component is missing or has no articles/provisions, falls back to the default lawset from CVar.
    /// </summary>
    public void GetEffectiveLawset(NetEntity? netUid,
        out List<ProtoId<CorporateLawPrototype>> provisions,
        out List<ProtoId<CorporateLawSectionPrototype>> articles,
        out List<ProtoId<CorporateLawPrototype>> circumstances,
        out int permanentThreshold)
    {
        var uid = GetEntity(netUid ?? NetEntity.Invalid);
        GetEffectiveLawset(uid.Valid ? uid : null, out provisions, out articles, out circumstances, out permanentThreshold);
    }

    public void GetEffectiveLawset(EntityUid? uid,
        out List<ProtoId<CorporateLawPrototype>> provisions,
        out List<ProtoId<CorporateLawSectionPrototype>> articles,
        out List<ProtoId<CorporateLawPrototype>> circumstances,
        out int permanentThreshold)
    {
        StationCorporateLawComponent? comp = null;
        if (uid != null)
        {
            var station = _station.GetOwningStation(uid.Value);
            TryComp(station, out comp);
        }

        // If component exists and has been initialized or has custom data, use its values.
        if (comp != null && (comp.Articles.Count > 0 || comp.Provisions.Count > 0 || comp.Circumstances.Count > 0 || comp.LawsetPrototype != null))
        {
            provisions = comp.Provisions;
            articles = comp.Articles;
            circumstances = comp.Circumstances;
            permanentThreshold = comp.PermanentSentenceThreshold;
            return;
        }

        // Fallback to CVar
        var lawsetId = _config.GetCVar(SunriseCCVars.SunriseCCVars.CorporateLawSet);
        if (_proto.TryIndex<CorporateLawsetPrototype>(lawsetId, out var proto))
        {
            provisions = proto.Provisions;
            articles = proto.Articles;
            circumstances = proto.Circumstances;
            permanentThreshold = proto.PermanentSentenceThreshold;
        }
        else
        {
            provisions = new();
            articles = new();
            circumstances = new();
            permanentThreshold = 50;
        }
    }

    public bool IsLawInEffectiveLawset(string lawId, EntityUid? station)
    {
        GetEffectiveLawset(station, out var provisions, out var sections, out _, out _);

        if (provisions.Any(p => (string) p == lawId))
            return true;

        foreach (var sId in sections)
        {
            if (_proto.TryIndex(sId, out var section) && section.Entries.Contains(lawId))
                return true;
        }

        return false;
    }

    public bool IsCircumstanceInEffectiveLawset(string circId, EntityUid? station)
    {
        GetEffectiveLawset(station, out _, out _, out var circumstances, out _);
        return circumstances.Contains(circId);
    }

    public bool IsLawInLawset(string lawId, StationCorporateLawComponent component)
    {
        // Check Articles
        foreach (var sectionId in component.Articles)
        {
            if (_proto.TryIndex(sectionId, out var section) && section.Entries.Contains(lawId))
                return true;
        }

        // Check Provisions
        if (component.Provisions.Any(p => (string) p == lawId))
            return true;

        return false;
    }

    public bool IsCircumstanceInLawset(string circId, StationCorporateLawComponent component)
    {
        return component.Circumstances.Contains(circId);
    }
}

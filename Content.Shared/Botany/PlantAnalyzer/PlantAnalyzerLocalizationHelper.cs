using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.PlantAnalyzer;


public sealed partial class PlantAnalyzerLocalizationHelper : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public string GasesToLocalizedStrings(List<Gas> gases)
    {
        if (gases.Count == 0)
            return "";

        List<string> gasesLoc = [];
        // Sunrise edit start - идентификаторы газов теперь строковые, поэтому Parse здесь небезопасен.
        foreach (var gas in gases)
        {
            var gasId = gas.ToString();
            gasesLoc.Add(_prototypeManager.TryIndex<GasPrototype>(gasId, out var prototype)
                ? Loc.GetString(prototype.Name)
                : gasId);
        }
        // Sunrise edit end

        return ContentLocalizationManager.FormatList(gasesLoc);
    }

    public string ChemicalsToLocalizedStrings(List<string> ids)
    {
        if (ids.Count == 0)
            return "";

        List<string> locStrings = [];
        foreach (var id in ids)
            locStrings.Add(_prototypeManager.TryIndex<ReagentPrototype>(id, out var prototype) ? prototype.LocalizedName : id);

        return ContentLocalizationManager.FormatList(locStrings);
    }

    public (string Singular, string Plural, string First) ProduceToLocalizedStrings(List<EntProtoId> ids)
    {
        if (ids.Count == 0)
            return ("", "", "");

        List<string> singularStrings = [];
        List<string> pluralStrings = [];
        foreach (var id in ids)
        {
            var singular = _prototypeManager.TryIndex(id, out var prototype) ? prototype.Name : id.Id;
            var plural = Loc.GetString("plant-analyzer-produce-plural", ("thing", singular));

            singularStrings.Add(singular);
            pluralStrings.Add(plural);
        }

        return (
            ContentLocalizationManager.FormatListToOr(singularStrings),
            ContentLocalizationManager.FormatListToOr(pluralStrings),
            singularStrings[0]
        );
    }
}

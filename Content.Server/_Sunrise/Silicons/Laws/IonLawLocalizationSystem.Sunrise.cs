using System.Globalization;

#pragma warning disable IDE0130 // Пространство имён vanilla-системы сохраняется для partial-расширения.
namespace Content.Server.Silicons.Laws;

public sealed partial class IonLawLocalizationSystem
{
    private static readonly string[] IonFunctionNames =
    [
        "ION-NUMBER-BASE",
        "ION-NUMBER-MOD",
        "ION-ADJECTIVE",
        "ION-SUBJECT",
        "ION-WHO",
        "ION-MUST",
        "ION-THING",
        "ION-JOB",
        "ION-WHO-GENERAL",
        "ION-PLURAL",
        "ION-REQUIRE",
        "ION-SEVERITY",
        "ION-ALLERGY",
        "ION-FEELING",
        "ION-CONCEPT",
        "ION-FOOD",
        "ION-DRINK",
        "ION-CHANGE",
        "ION-WHO-RANDOM",
        "ION-AREA",
        "ION-PART",
        "ION-OBJECT",
        "ION-HARM-PROTECT",
        "ION-VERB",
    ];

    private void RegisterFallbackIonFunctions(CultureInfo defaultCulture)
    {
        foreach (var culture in _loc.GetFoundCultures())
        {
            if (!_loc.HasCulture(culture) || culture.Name == defaultCulture.Name)
                continue;

            foreach (var functionName in IonFunctionNames)
                _loc.AddFunction(culture, functionName, _ => GetIonLawValue(functionName));
        }
    }
}

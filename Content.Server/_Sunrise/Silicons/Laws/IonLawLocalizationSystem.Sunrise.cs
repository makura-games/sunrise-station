using System.Globalization;

// ReSharper disable once CheckNamespace
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
        foreach (var culture in Loc.GetFoundCultures())
        {
            if (!Loc.HasCulture(culture) || culture.Name == defaultCulture.Name)
                continue;

            foreach (var functionName in IonFunctionNames)
                Loc.AddFunction(culture, functionName, _ => GetIonLawValue(functionName));
        }
    }
}

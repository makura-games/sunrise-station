using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Smell.Prototypes;

/// <summary>
/// Single source of scent prototype ids used by the server-side smell logic
/// (references into scents.yml). Item emitter and status-scent ids are not
/// duplicated here — those are set directly in the item YAML.
/// </summary>
public static class ScentIds
{
    /// <summary>The entity's own blood, smelled once wound damage crosses the threshold.</summary>
    [ValidatePrototypeId<ScentPrototype>]
    public const string Blood = "SunriseBlood";

    /// <summary>Victim's blood smeared onto the attacker finishing off a critical target.</summary>
    [ValidatePrototypeId<ScentPrototype>]
    public const string OtherBlood = "SunriseOtherBlood";

    /// <summary>Adrenaline sweat smelled once blunt damage crosses the threshold.</summary>
    [ValidatePrototypeId<ScentPrototype>]
    public const string Bruise = "SunriseBruise";

    /// <summary>Toxic odor smelled once poison damage crosses the threshold.</summary>
    [ValidatePrototypeId<ScentPrototype>]
    public const string Poison = "SunrisePoison";

    /// <summary>Smoke smell from burning tobacco products.</summary>
    [ValidatePrototypeId<ScentPrototype>]
    public const string Smoke = "SunriseSmoke";
}

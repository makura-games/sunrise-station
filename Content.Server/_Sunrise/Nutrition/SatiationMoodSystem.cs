using Content.Server._Sunrise.Mood;
using Content.Shared._Sunrise.Mood;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Configuration;

namespace Content.Server._Sunrise.Nutrition;

/// <summary>
/// Синхронизирует эффекты настроения Sunrise с универсальной системой насыщения.
/// </summary>
public sealed partial class SatiationMoodSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private SatiationSystem _satiation = default!;

    private static readonly Dictionary<SatiationValue, string> HungerEffects = new()
    {
        ["Overfed"] = "HungerOverfed",
        ["Okay"] = "HungerOkay",
        ["Peckish"] = "HungerPeckish",
        ["Starving"] = "HungerStarving",
    };

    private static readonly Dictionary<SatiationValue, string> ThirstEffects = new()
    {
        ["Overhydrated"] = "ThirstOverHydrated",
        ["Okay"] = "ThirstOkay",
        ["Thirsty"] = "ThirstThirsty",
        ["Parched"] = "ThirstParched",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MoodComponent, SatiationUpdateEvent>(OnSatiationUpdated);
    }

    private void OnSatiationUpdated(Entity<MoodComponent> entity, ref SatiationUpdateEvent args)
    {
        if (!_configuration.GetCVar(SunriseCCVars.MoodEnabled) ||
            !TryComp<SatiationComponent>(entity, out var satiation))
        {
            return;
        }

        var effects = args.Type == SatiationSystem.Hunger
            ? HungerEffects
            : args.Type == SatiationSystem.Thirst
                ? ThirstEffects
                : null;

        if (effects == null ||
            !_satiation.TryGetValueByThreshold((entity, satiation), args.Type, effects, out var effect, out _, out _) ||
            effect == null)
        {
            return;
        }

        RaiseLocalEvent(entity, new MoodEffectEvent(effect));
    }
}

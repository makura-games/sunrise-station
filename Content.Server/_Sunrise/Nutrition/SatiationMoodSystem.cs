using Content.Server._Sunrise.Mood;
using Content.Shared._Sunrise.Mood;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Nutrition;

/// <summary>
/// Синхронизирует эффекты настроения Sunrise с универсальной системой насыщения.
/// </summary>
public sealed partial class SatiationMoodSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Dictionary<SatiationValue, string> HungerEffects = new()
    {
        ["Overfed"] = "HungerOkay",
        ["Okay"] = "HungerOkay",
        ["Peckish"] = "HungerPeckish",
        ["Starving"] = "HungerStarving",
    };

    private static readonly Dictionary<SatiationValue, string> ThirstEffects = new()
    {
        ["Overhydrated"] = "ThirstOkay",
        ["Okay"] = "ThirstOkay",
        ["Thirsty"] = "ThirstThirsty",
        ["Parched"] = "ThirstParched",
    };

    private TimeSpan _nextUpdate;

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

        UpdateEffect(entity, satiation, args.Type);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        if (!_configuration.GetCVar(SunriseCCVars.MoodEnabled))
            return;

        // Насыщение убывает по времени без SatiationUpdateEvent на каждом пороге настроения.
        var query = EntityQueryEnumerator<MoodComponent, SatiationComponent>();
        while (query.MoveNext(out var uid, out var mood, out var satiation))
        {
            UpdateEffect((uid, mood), satiation, SatiationSystem.Hunger);
            UpdateEffect((uid, mood), satiation, SatiationSystem.Thirst);
        }
    }

    private void UpdateEffect(Entity<MoodComponent> entity, SatiationComponent satiation,
        ProtoId<SatiationTypePrototype> type)
    {
        var effects = type == SatiationSystem.Hunger
            ? HungerEffects
            : type == SatiationSystem.Thirst
                ? ThirstEffects
                : null;

        if (effects == null ||
            !_satiation.TryGetValueByThreshold((entity, satiation), type, effects, out var effect, out _, out _) ||
            effect == null)
        {
            return;
        }

        if (!entity.Comp.CategorisedEffects.ContainsValue(effect))
            RaiseLocalEvent(entity, new MoodEffectEvent(effect));
    }
}

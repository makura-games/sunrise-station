using Content.Server.StationEvents.Components;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;
using Content.Shared.Radiation.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Tag;
using Content.Shared.Stacks;
using Content.Shared.Ghost;

namespace Content.Server.StationEvents.Events;

public sealed partial class RadiationOutburstRuleSystem : StationEventSystem<RadiationOutburstRuleComponent> //port only with codeowner permision @4_ydo
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private TagSystem _tagSystem = default!;

    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private static readonly ProtoId<TagPrototype> HighRiskItemTag = "HighRiskItem";

    public override void Initialize()
    {
        base.Initialize();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
    }

    protected override void Started(EntityUid uid, RadiationOutburstRuleComponent component,
        GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        if (!TryGetRandomStation(out var station))
            return;

        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<ItemComponent, TransformComponent>();

        while (query.MoveNext(out var targetUid, out _, out var xform))
        {
            // не на обломке каком-нибудь
            if (StationSystem.GetOwningStation(targetUid, xform) != station)
                continue;

            // анти вещи плееров
            if (IsInsideLivingEntity(targetUid))
                continue;

            // анти хайриск хуйня
            if (_tagSystem.HasTag(targetUid, HighRiskItemTag))
                continue;

            // анти стаки
            if (HasComp<StackComponent>(targetUid))
                continue;

            // анти педали айтем
            if (_ghostQuery.HasComponent(targetUid))
                continue;

            candidates.Add(targetUid);
        }

        if (candidates.Count == 0)
        {
            Log.Info("RadiationOutburst: Нет предметов для облучения");
            return;
        }

        _random.Shuffle(candidates);

        var itemsToIrradiate = Math.Min(8, candidates.Count);
        for (int i = 0; i < itemsToIrradiate; i++)
        {
            var target = candidates[i];
            var rads = _random.Next(2, 3); // Интенсивность 2-3
            SetRadiation(target, rads);
        }
    }

    private bool IsInsideLivingEntity(EntityUid entity)
    {
        if (!_containerSystem.TryGetContainingContainer(entity, out var container))
            return false;

        return _mobStateQuery.HasComponent(container.Owner);
    }

    private void SetRadiation(EntityUid target, float rads)
    {
        var radiationComp = EnsureComp<RadiationSourceComponent>(target);
        Log.Debug($"RadiationOutburst: {target} теперь излучает +{rads} (всего: {radiationComp.Intensity})");
    }
}
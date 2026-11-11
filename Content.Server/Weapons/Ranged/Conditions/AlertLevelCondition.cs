using Content.Shared.AlertLevel;
using Content.Shared.Station.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Weapons.Ranged.Conditions;

public sealed partial class AlertLevelCondition : FireModeCondition
{
    [DataField(required: true)]
    public List<ProtoId<AlertLevelPrototype>> AlertLevels = [];

    public override bool Condition(FireModeConditionConditionArgs args)
    {
        var entityManager = args.EntityManager;

        var alertSystem = entityManager.System<AlertLevelSystem>();

        if (!entityManager.TryGetComponent<TransformComponent>(args.Shooter, out var transformComp))
            return false;

        if (entityManager.TryGetComponent<StationMemberComponent>(transformComp.ParentUid, out var stationMember) &&
            alertSystem.TryGetLevel(stationMember.Station, out var alertLevel) &&
            alertLevel is { } level)
        {
            return AlertLevels.Contains(level);
        }

        return false;
    }
}

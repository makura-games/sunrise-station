using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared.Nutrition.EntitySystems;

public sealed partial class SatiationSpeedModifierSystem :
    BaseSatiationEffectSystem<SatiationSpeedModifierComponent, float>
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private SharedJetpackSystem _jetpack = default!; // Sunrise-Edit
    [Dependency] private IConfigurationManager _configuration = default!; // Sunrise-Edit

    protected override Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<float>> GetThresholds(
        SatiationSpeedModifierComponent comp) => comp.Satiations;

    protected override float DefaultValue() => 1f;

    protected override void AfterSatiationUpdate(Entity<SatiationSpeedModifierComponent> entity)
    {
        _movementSpeedModifier.RefreshMovementSpeedModifiers(entity.Owner);
    }

    [SubscribeLocalEvent]
    private void OnRefreshMovementSpeed(
        Entity<SatiationSpeedModifierComponent> entity,
        ref RefreshMovementSpeedModifiersEvent args
    )
    {
        // Sunrise-Edit - при включённом настроении потребности влияют на настроение вместо скорости.
        if (_configuration.GetCVar(SunriseCCVars.MoodEnabled) || _jetpack.IsUserFlying(entity.Owner))
            return;

        foreach (var (_, thresholds) in entity.Comp.Satiations)
        {
            args.ModifySpeed(thresholds.Current);
        }
    }
}

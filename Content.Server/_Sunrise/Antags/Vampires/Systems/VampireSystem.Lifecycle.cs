using Content.Server.Body.Components;
using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Body;
using Content.Shared.Metabolism;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Создание и удаление состояния вампира.

    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly MetabolizerSystem _metabolizer = default!;


    private void OnStartup(Entity<VampireComponent> ent, ref ComponentStartup args)
    {
        var configuration = EnsureComp<VampireConfigurationComponent>(ent);
        EnsureComp<VampireFeedingComponent>(ent);
        EnsureComp<VampireHolyComponent>(ent);

        SetVampireMetabolism(ent, configuration, enabled: true);
        UpdatePowerLevel(ent, syncActions: false);
        ApplyPowerLevelSettings(ent);
        GrantBaseActions(ent, configuration);

        RemComp<HungerComponent>(ent);
        RemComp<ThirstComponent>(ent);
        RemComp<RespiratorComponent>(ent);

        _alerts.ClearAlertCategory(ent.Owner, configuration.HungerAlertCategory);
        UpdateVampireAlert(ent);
        UpdateVampireFedAlert(ent);

        SyncVampireActions(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnShutdown(Entity<VampireComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<VampireConfigurationComponent>(ent, out var configuration))
            SetVampireMetabolism(ent, configuration, enabled: false);

        RemoveVampireActions(ent);

        RemCompDeferred<VampireFeedingComponent>(ent);
        RemCompDeferred<VampireHolyComponent>(ent);
        RemCompDeferred<ActiveVampireRejuvenateComponent>(ent);
        RemCompDeferred<VampireConfigurationComponent>(ent);
    }

    private void OnSetVampireMetabolism(
        Entity<MetabolizerComponent> ent,
        ref BodyRelayedEvent<SetVampireMetabolismEvent> args)
    {
        if (args.Args.Enabled)
            _metabolizer.TryAddMetabolizerType(ent, args.Args.MetabolizerType);
        else
            _metabolizer.TryRemoveMetabolizerType(ent, args.Args.MetabolizerType);
    }

    private void SetVampireMetabolism(
        Entity<VampireComponent> ent,
        VampireConfigurationComponent configuration,
        bool enabled)
    {
        if (!TryComp<BodyComponent>(ent, out var body))
            return;

        var ev = new SetVampireMetabolismEvent(enabled, configuration.MetabolizerType);
        _body.RelayEvent((ent.Owner, body), ref ev);
    }

    private readonly record struct SetVampireMetabolismEvent(
        bool Enabled,
        ProtoId<MetabolizerTypePrototype> MetabolizerType);
}

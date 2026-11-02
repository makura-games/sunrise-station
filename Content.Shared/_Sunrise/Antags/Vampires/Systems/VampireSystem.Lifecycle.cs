using Content.Shared._Sunrise.Antags.Vampires.Components;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Создание состояния на сервере и восстановление производных данных после отката.

    [SubscribeLocalEvent]
    public void OnStartup(Entity<VampireComponent> ent, ref ComponentStartup args)
    {
        // Клиент получает компоненты и действия с сервера, а не создаёт их при применении снимка состояния.
        if (_net.IsClient)
            return;

        var configuration = EnsureComp<VampireConfigurationComponent>(ent);
        EnsureComp<VampireFeedingComponent>(ent);
        UpdatePowerLevel(ent, syncActions: false);
        ApplyPowerLevelSettings(ent);
        GrantBaseActions(ent, configuration);
        SyncVampireActions(ent);
        UpdateVampireAlert(ent);
        UpdateVampireFedAlert(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    [SubscribeLocalEvent]
    public void OnShutdown(Entity<VampireComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        RemoveVampireActions(ent);
        RemCompDeferred<VampireFeedingComponent>(ent);
        RemCompDeferred<ActiveVampireRejuvenateComponent>(ent);
        RemCompDeferred<VampireConfigurationComponent>(ent);
    }

    [SubscribeLocalEvent]
    public void OnAfterState(Entity<VampireComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }
}

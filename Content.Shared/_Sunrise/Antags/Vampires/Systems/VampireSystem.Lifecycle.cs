using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;

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
        _actionGrant.AddActions((ent.Owner, null), configuration.BaseActions);
        UpdateVampireAlert(ent);
        UpdateVampireFedAlert(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

        var ev = new VampireStartupEvent();
        RaiseLocalEvent(ent, ref ev);
    }

    [SubscribeLocalEvent]
    public void OnShutdown(Entity<VampireComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        var ev = new VampireShutdownEvent();
        RaiseLocalEvent(ent, ref ev);

        if (TryComp<VampireConfigurationComponent>(ent, out var configuration))
            _actionGrant.RemoveActions((ent.Owner, null), configuration.BaseActions);

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

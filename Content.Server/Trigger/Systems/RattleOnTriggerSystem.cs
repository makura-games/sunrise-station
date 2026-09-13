using Content.Server.Radio.EntitySystems;
using Content.Server.Pinpointer;
using Content.Server.Station.Systems;
using Content.Server._Sunrise.Messenger;
using Content.Shared.Mobs.Components;
using Content.Shared.Radio;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Trigger.Systems;

public sealed partial class RattleOnTriggerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private MessengerServerSystem _messenger = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RattleOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<RattleOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        if (!TryComp<MobStateComponent>(target.Value, out var mobstate))
            return;

        args.Handled = true;

        if (!ent.Comp.Messages.TryGetValue(mobstate.CurrentState, out var messageId))
            return;

        // Gets the location of the user
        var posText = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(target.Value));

        var message = Loc.GetString(messageId, ("user", target.Value), ("position", posText));

        // Sunrise-Start
        var sentToMessenger = false;
        if (_messenger.GetServerEntity(_station.GetOwningStation(ent.Owner)) is var (server, _) &&
            _messenger.GetGroupIdByRadioChannel(ent.Comp.RadioChannel) is { } groupId)
        {
            _messenger.SendSystemMessageToGroup(server, groupId, message);
            sentToMessenger = true;
        }

        if (!sentToMessenger)
            _radio.SendRadioMessage(ent.Owner, message, _prototypeManager.Index(ent.Comp.RadioChannel), ent.Owner);
        // Sunrise-End
    }
}

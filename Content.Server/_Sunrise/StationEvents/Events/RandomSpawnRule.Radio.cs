using Content.Server.Pinpointer;
using Content.Server.Radio.EntitySystems;
using Content.Server.StationEvents.Components;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Пространство имён намеренно соответствует расширяемой vanilla-системе.
namespace Content.Server.StationEvents.Events;

public sealed partial class RandomSpawnRule
{
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly RadioSystem _radio = default!;

    private void SendRadioAnnouncement(EntityUid spawned, RandomSpawnRuleComponent component)
    {
        if (component.RadioMessage is not { } radioMessage)
            return;

        var location = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(spawned));
        var message = Loc.GetString(radioMessage.Message, ("location", location));
        _radio.SendRadioMessage(spawned, message, radioMessage.Channel, spawned);
    }
}

using System.Linq;
using Content.Server._Sunrise.AnnouncementSpeaker;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Robust.Shared.Player;
using Content.Shared._Sunrise.CollectiveMind;
using Robust.Shared.Utility;
using Robust.Shared.Audio;
using Content.Server._Sunrise.Chat;
using Content.Shared.Database;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    [Dependency] private AnnouncementSpeakerSystem _announcementSpeaker = default!;

    public const string DefaultSunriseAnnouncementSound = "/Audio/_Sunrise/Announcements/announce_dig.ogg";

    private void SendCollectiveMindChat(EntityUid source, string message, CollectiveMindPrototype? collectiveMind)
    {
        if (_mobStateSystem.IsDead(source))
            return;

        if (collectiveMind == null || message == "")
            return;

        if (!TryComp<CollectiveMindComponent>(source, out var sourseCollectiveMindComp))
            return;

        if (!sourseCollectiveMindComp.Minds.Contains(collectiveMind.ID))
            return;

        var clients = Filter.Empty();
        var receivers = new HashSet<EntityUid>();
        var mindQuery = EntityQueryEnumerator<CollectiveMindComponent, ActorComponent>();
        while (mindQuery.MoveNext(out var uid, out var collectMindComp, out var actorComp))
        {
            if (_mobStateSystem.IsDead(uid))
                continue;

            if (collectMindComp.Minds.Contains(collectiveMind.ID))
            {
                clients.AddPlayer(actorComp.PlayerSession);
                receivers.Add(uid);
            }
        }

        var admins = _adminManager.ActiveAdmins
            .Select(p => p.Channel);
        string messageWrap;
        string adminMessageWrap;

        if (collectiveMind.ShowAuthor)
        {
            messageWrap = Loc.GetString("collective-mind-chat-wrap-message-with-author",
                ("source", source),
                ("message", message),
                ("channel", collectiveMind.LocalizedName));
        }
        else
        {
            messageWrap = Loc.GetString("collective-mind-chat-wrap-message",
                ("message", message),
                ("channel", collectiveMind.LocalizedName));
        }

        adminMessageWrap = Loc.GetString("collective-mind-chat-wrap-message-admin",
            ("source", source),
            ("message", message),
            ("channel", collectiveMind.LocalizedName));

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"CollectiveMind chat from {ToPrettyString(source):Player}: {message}");

        _chatManager.ChatMessageToManyFiltered(clients,
            ChatChannel.CollectiveMind,
            message,
            messageWrap,
            source,
            false,
            true,
            collectiveMind.Color);

        _chatManager.ChatMessageToMany(ChatChannel.CollectiveMind,
            message,
            adminMessageWrap,
            source,
            false,
            true,
            admins,
            collectiveMind.Color);

        // Raise event for TTS
        RaiseLocalEvent(new CollectiveMindSpokeEvent(source, message, receivers, collectiveMind.ID));
    }

    /// <summary>
    /// Gets all players who have working announcement speakers nearby.
    /// Used to filter chat recipients for announcements.
    /// </summary>
    private Filter GetPlayersWithWorkingSpeakers()
    {
        var filteredPlayers = Filter.Empty();

        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            if (_announcementSpeaker.HasWorkingSpeakersNearby(playerEntity))
            {
                filteredPlayers = filteredPlayers.AddPlayer(player);
            }
        }

        return filteredPlayers;
    }

    /// <summary>
    /// Filters an existing filter to only include players with working speakers nearby.
    /// </summary>
    private Filter FilterPlayersByWorkingSpeakers(Filter originalFilter)
    {
        var filteredPlayers = Filter.Empty();

        foreach (var player in originalFilter.Recipients)
        {
            if (player.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            if (_announcementSpeaker.HasWorkingSpeakersNearby(playerEntity))
            {
                filteredPlayers = filteredPlayers.AddPlayer(player);
            }
        }

        return filteredPlayers;
    }
}

public sealed class RadioSpokeEvent(EntityUid source, string message, EntityUid[] receivers, string channelId) : EntityEventArgs
{
    public readonly EntityUid Source = source;
    public readonly string Message = message;
    public readonly EntityUid[] Receivers = receivers;
    public readonly string ChannelId = channelId;
}

public sealed class CollectiveMindSpokeEvent(EntityUid source, string message, IReadOnlyCollection<EntityUid> receivers, string collectiveMindId) : EntityEventArgs
{
    public readonly EntityUid Source = source;
    public readonly string Message = message;
    public readonly IReadOnlyCollection<EntityUid> Receivers = receivers;
    public readonly string CollectiveMindId = collectiveMindId;
}

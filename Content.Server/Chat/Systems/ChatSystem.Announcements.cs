using System.Linq;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    /// <inheritdoc />
    public override void DispatchGlobalAnnouncement(
        string message,
        string? sender = null,
        bool playDefault = true,
        SoundSpecifier? announcementSound = null,
        bool playTts = true, // Sunrise-Edit
        ProtoId<TTSVoicePrototype>? announceVoice = null, // Sunrise-Edit
        Color? colorOverride = null
        )
    {
        sender ??= Loc.GetString("chat-manager-sender-announcement");

        var wrappedMessage = Loc.GetString("chat-manager-sender-announcement-wrap-message", ("sender", sender), ("message", FormattedMessage.EscapeText(message)));

        // Sunrise edit start - объявления получают игроки рядом с рабочими динамиками
        var filteredPlayers = GetPlayersWithWorkingSpeakers();
        if (filteredPlayers.Recipients.Any())
            _chatManager.ChatMessageToManyFiltered(filteredPlayers, ChatChannel.Radio, message, wrappedMessage, default, false, true, colorOverride);
        // Sunrise edit end

        // Sunrise edit start - воспроизведение объявлений через сеть динамиков
        if (playTts && (playDefault || announcementSound != null))
        {
            if (playDefault && announcementSound == null)
                announcementSound = new SoundPathSpecifier(DefaultSunriseAnnouncementSound);

            _announcementSpeaker.DispatchAnnouncementToAllStations(message, announcementSound, announceVoice?.Id);
        }
        // Sunrise edit end

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Global station announcement from {sender}: {message}");
    }

    /// <inheritdoc />
    public override void DispatchFilteredAnnouncement(
        Filter filter,
        string message,
        EntityUid? source = null,
        string? sender = null,
        bool playDefault = true,
        bool playTts = true, // Sunrise-Edit
        ProtoId<TTSVoicePrototype>? announceVoice = null, // Sunrise-Edit
        SoundSpecifier? announcementSound = null,
        Color? colorOverride = null)
    {
        sender ??= Loc.GetString("chat-manager-sender-announcement");

        var wrappedMessage = Loc.GetString("chat-manager-sender-announcement-wrap-message", ("sender", sender), ("message", FormattedMessage.EscapeText(message)));

        // Sunrise edit start - объявления получают игроки рядом с рабочими динамиками
        var filteredPlayers = FilterPlayersByWorkingSpeakers(filter);
        if (filteredPlayers.Recipients.Any())
            _chatManager.ChatMessageToManyFiltered(filteredPlayers, ChatChannel.Radio, message, wrappedMessage, source ?? default, false, true, colorOverride);
        // Sunrise edit end

        // Sunrise edit start - воспроизведение объявлений через сеть динамиков
        if (playTts && (playDefault || announcementSound != null))
        {
            if (playDefault && announcementSound == null)
                announcementSound = new SoundPathSpecifier(DefaultSunriseAnnouncementSound);

            if (source != null && _stationSystem.GetOwningStation(source.Value) is { } station)
                _announcementSpeaker.DispatchAnnouncementToSpeakers(station, message, announcementSound, announceVoice?.Id);
            else
                _announcementSpeaker.DispatchAnnouncementToAllStations(message, announcementSound, announceVoice?.Id);
        }
        // Sunrise edit end

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Station Announcement from {sender}: {message}");
    }

    /// <inheritdoc />
    public override void DispatchStationAnnouncement(
        EntityUid source,
        string message,
        string? sender = null,
        bool playDefault = true, // Sunrise-Edit
        bool playTts = true, // Sunrise-Edit
        ProtoId<TTSVoicePrototype>? announceVoice = null, // Sunrise-Edit
        bool playDefaultSound = true,
        SoundSpecifier? announcementSound = null,
        Color? colorOverride = null)
    {
        sender ??= Loc.GetString("chat-manager-sender-announcement");

        var wrappedMessage = Loc.GetString("chat-manager-sender-announcement-wrap-message", ("sender", sender), ("message", FormattedMessage.EscapeText(message)));
        var station = _stationSystem.GetOwningStation(source);

        if (station == null)
        {
            // you can't make a station announcement without a station
            return;
        }

        if (!TryComp<StationDataComponent>(station, out var stationDataComp)) return;

        var filter = _stationSystem.GetInStation(stationDataComp);

        // Sunrise edit start - объявления получают игроки рядом с рабочими динамиками
        var filteredPlayers = FilterPlayersByWorkingSpeakers(filter);
        if (filteredPlayers.Recipients.Any())
            _chatManager.ChatMessageToManyFiltered(filteredPlayers, ChatChannel.Radio, message, wrappedMessage, source, false, true, colorOverride);
        // Sunrise edit end

        // Sunrise edit start - воспроизведение объявлений через сеть динамиков
        if (playTts)
        {
            if (playDefault && announcementSound == null)
                announcementSound = new SoundPathSpecifier(DefaultSunriseAnnouncementSound);

            _announcementSpeaker.DispatchAnnouncementToSpeakers(station.Value, message, announcementSound, announceVoice?.Id);
        }
        // Sunrise edit end

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Station Announcement on {station} from {sender}: {message}");
    }
}

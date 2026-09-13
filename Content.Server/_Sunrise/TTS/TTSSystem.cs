using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Content.Server._Sunrise.AnnouncementSpeaker;
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Shared._Sunrise.CollectiveMind;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Radio.Components;
using Content.Shared._Sunrise.AnnouncementSpeaker.Components;
using Content.Shared._Sunrise.AnnouncementSpeaker.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Chat;

namespace Content.Server._Sunrise.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private TTSManager _ttsManager = default!;
    [Dependency] private SharedTransformSystem _xforms = default!;
    [Dependency] private IRobustRandom _rng = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private AnnouncementSpeakerSystem _announcementSpeakerSystem = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    private readonly List<string> _sampleText =
        new()
        {
            "Съешь же ещё этих мягких французских булок, да выпей чаю.",
            "Клоун, прекрати разбрасывать банановые кожурки офицерам под ноги!",
            "Капитан, вы уверены что хотите назначить клоуна на должность главы персонала?",
            "Эс Бэ! Тут человек в сером костюме, с тулбоксом и в маске! Помогите!!",
            "Учёные, тут странная аномалия в баре! Она уже съела мима!",
            "Я надеюсь что инженеры внимательно следят за сингулярностью...",
            "Вы слышали эти странные крики в техах? Мне кажется туда ходить небезопасно.",
            "Вы не видели Гамлета? Мне кажется он забегал к вам на кухню.",
            "Здесь есть доктор? Человек умирает от отравленного пончика! Нужна помощь!",
            "Вам нужно согласие и печать квартирмейстера, если вы хотите сделать заказ на партию дробовиков.",
            "Возле эвакуационного шаттла разгерметизация! Инженеры, нам срочно нужна ваша помощь!",
            "Бармен, налей мне самого крепкого вина, которое есть в твоих запасах!"
        };


    private const int MaxMessageChars = 100 * 2; // same as SingleBubbleCharLimit * 2
    private bool _isEnabled;
    private string _defaultAnnounceVoice = "Hanson";
    private List<ICommonSession> _ignoredRecipients = new();
    private const float AnnouncementTtsVolumeModifier = 0.68f; // громкость объявлений в динамиках по сравнению с обычной речью
    private const int MaxAnnouncementTtsSources = 4;
    private const float WhisperVoiceVolumeModifier = 0.6f; // how far whisper goes in world units
    private const int WhisperVoiceRange = 3; // how far whisper goes in world units
    private string _radioEffect = string.Empty;

    public override void Initialize()
    {
        _cfg.OnValueChanged(SunriseCCVars.TTSEnabled, v => _isEnabled = v, true);
        _cfg.OnValueChanged(SunriseCCVars.TTSRadioEffect, OnRadioEffectChanged, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioReceiveEvent);
        SubscribeLocalEvent<CollectiveMindSpokeEvent>(OnCollectiveMindSpokeEvent);
        SubscribeLocalEvent<AnnouncementSpeakerEvent>(OnAnnouncementSpeaker);

        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);
        SubscribeNetworkEvent<ClientOptionTTSEvent>(OnClientOptionTTS);
    }

    private void OnRadioEffectChanged(string value)
    {
        _radioEffect = value;
    }

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice);
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));
    }

    private async void OnClientOptionTTS(ClientOptionTTSEvent ev, EntitySessionEventArgs args)
    {
        if (ev.Enabled)
            _ignoredRecipients.Remove(args.SenderSession);
        else
            _ignoredRecipients.Add(args.SenderSession);
    }

    private void OnRadioReceiveEvent(RadioSpokeEvent args)
    {
        if (!_isEnabled || args.Message.Length > MaxMessageChars)
            return;

        if (!TryComp(args.Source, out TTSComponent? senderComponent))
            return;

        var voiceId = senderComponent.VoicePrototypeId;
        if (voiceId == null || string.IsNullOrWhiteSpace(voiceId.Value))
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(args.Source, voiceId.Value);
        RaiseLocalEvent(args.Source, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!GetVoicePrototype(voiceId, out var protoVoice))
        {
            return;
        }

        var accentEvent = new TTSSanitizeEvent(args.Message);
        RaiseLocalEvent(args.Source, accentEvent);
        var message = accentEvent.Text;

        HandleRadio(args.Receivers, message, protoVoice, args.ChannelId, voiceEv.Effect);
    }

    private async void OnCollectiveMindSpokeEvent(CollectiveMindSpokeEvent args)
    {
        if (!_isEnabled || args.Message.Length > MaxMessageChars)
            return;

        // Get the collective mind prototype to use its voice
        if (!_prototypeManager.TryIndex<CollectiveMindPrototype>(args.CollectiveMindId, out var collectiveMindProto))
            return;

        var voiceId = collectiveMindProto.VoiceId;
        if (voiceId == null || string.IsNullOrWhiteSpace(voiceId.Value))
            return;

        if (!GetVoicePrototype(voiceId, out var protoVoice))
        {
            return;
        }

        var accentEvent = new TTSSanitizeEvent(args.Message);
        RaiseLocalEvent(args.Source, accentEvent);
        var message = accentEvent.Text;

        var soundData = await GenerateTTS(message, protoVoice);
        if (soundData is null)
            return;

        var recipients = Filter.Entities(args.Receivers.ToArray()).RemovePlayers(_ignoredRecipients);
        RaiseNetworkEvent(new PlayTTSEvent(soundData, null, false), recipients);
    }

    private bool GetVoicePrototype(ProtoId<TTSVoicePrototype>? voiceId, [NotNullWhen(true)] out TTSVoicePrototype? voicePrototype)
    {
        if (!_prototypeManager.TryIndex(voiceId, out voicePrototype))
        {
            return _prototypeManager.TryIndex(_defaultAnnounceVoice, out voicePrototype);
        }

        return true;
    }

    /// <summary>
    /// Handles station-wide announcements by finding all speakers on the station and playing the announcement through them.
    /// </summary>
    private void OnAnnouncementSpeaker(ref AnnouncementSpeakerEvent ev)
    {
        // Find all speakers on the station
        var speakers = _announcementSpeakerSystem.GetStationSpeakers(ev.Station);

        if (speakers.Count == 0)
        {
            // Fallback: If no speakers are found, log a warning
            // In the future, this could send to a single communications console or similar
            Logger.Warning($"No announcement speakers found on station {ToPrettyString(ev.Station)}. Announcement not played: {ev.Message}");
            return;
        }

        // Play announcement sound via PVS for each speaker on server side
        if (ev.AnnouncementSound != null)
        {
            foreach (var speaker in speakers)
            {
                if (!TryComp<AnnouncementSpeakerComponent>(speaker, out var speakerComp))
                    continue;

                // Check if speaker is enabled and has power
                if (!speakerComp.Enabled)
                    continue;

                if (speakerComp.RequiresPower)
                {
                    if (!TryComp<ApcPowerReceiverComponent>(speaker, out var powerReceiver) || !powerReceiver.Powered)
                        continue;
                }

                // Play announcement sound via PVS from this speaker
                var audioParams = (ev.AnnouncementSoundParams ?? AudioParams.Default).WithMaxDistance(speakerComp.Range);

                if (speakerComp.VolumeModifier <= 0f)
                    audioParams = audioParams.WithVolume(float.NegativeInfinity);

                else if (speakerComp.VolumeModifier != 1f)
                    audioParams = audioParams.AddVolume(SharedAudioSystem.GainToVolume(speakerComp.VolumeModifier));

                _audioSystem.PlayPvs(ev.AnnouncementSound, speaker, audioParams);
            }
        }

        if (ev.TtsData == null || ev.TtsData.Length <= 0)
            return;

        var speakerData = new List<(MapCoordinates Coordinates, AnnouncementSpeakerComponent Comp)>();
        foreach (var speaker in speakers)
        {
            if (!TryComp<AnnouncementSpeakerComponent>(speaker, out var speakerComp))
                continue;
            if (!speakerComp.Enabled || speakerComp.VolumeModifier <= 0f)
                continue;
            if (speakerComp.RequiresPower)
            {
                if (!TryComp<ApcPowerReceiverComponent>(speaker, out var powerReceiver) || !powerReceiver.Powered)
                    continue;
            }
            speakerData.Add((_xforms.GetMapCoordinates(speaker), speakerComp));
        }

        // Для каждого игрока на станции определяем, какие динамики он слышит
        var playerQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (playerQuery.MoveNext(out var playerUid, out var actor, out var playerXform))
        {
            if (_ignoredRecipients.Contains(actor.PlayerSession))
                continue;

            var playerCoordinates = _xforms.GetMapCoordinates(playerXform);
            var heardSpeakers = speakerData
                .Where(speaker => speaker.Coordinates.MapId == playerCoordinates.MapId)
                .Select(speaker => (
                    speaker.Coordinates,
                    speaker.Comp,
                    DistanceSquared: (speaker.Coordinates.Position - playerCoordinates.Position).LengthSquared()))
                .Where(speaker => speaker.DistanceSquared <= speaker.Comp.Range * speaker.Comp.Range)
                .OrderBy(speaker => speaker.DistanceSquared)
                .Take(MaxAnnouncementTtsSources)
                .Select(speaker => new MultiSpeakerTtsSource(
                    speaker.Coordinates,
                    speaker.Comp.VolumeModifier,
                    speaker.Comp.Range))
                .ToList();

            if (heardSpeakers.Count > 0)
            {
                var evMulti = new PlayMultiSpeakerTTSEvent(heardSpeakers, ev.TtsData, volumeModifier: AnnouncementTtsVolumeModifier);
                RaiseNetworkEvent(evMulti, actor.PlayerSession);
            }
        }
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars ||
            voiceId == null || string.IsNullOrWhiteSpace(voiceId.Value))
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId.Value);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!GetVoicePrototype(voiceId, out var protoVoice))
        {
            return;
        }

        var accentEvent = new TTSSanitizeEvent(args.Message);
        RaiseLocalEvent(uid, accentEvent);
        var message = accentEvent.Text;

        if (args.ObfuscatedMessage != null)
        {
            HandleWhisper(uid, message, protoVoice);
            return;
        }

        HandleSay(uid, message, protoVoice, voiceEv.Effect);
    }

    private async void HandleSay(EntityUid uid, string message, TTSVoicePrototype voicePrototype, string? effect)
    {

        var recipients = Filter.Pvs(uid, 1F).RemovePlayers(_ignoredRecipients);

        // Если нету получателей TTS, то зачем вообще генерировать его?
        if (!recipients.Recipients.Any())
            return;

        var soundData = await GenerateTTS(message, voicePrototype, effect);

        if (soundData is null)
            return;

        var netEntity = GetNetEntity(uid);

        RaiseNetworkEvent(new PlayTTSEvent(soundData, netEntity), recipients);
    }

    private async void HandleWhisper(EntityUid uid, string message, TTSVoicePrototype voicePrototype)
    {
        // If it's a whisper into a radio, generate speech without whisper
        // attributes to prevent an additional speech synthesis event
        var soundData = await GenerateTTS(message, voicePrototype);
        if (soundData is null)
            return;

        // TODO: Check obstacles
        var sourcePos = _xforms.GetWorldPosition(_xformQuery.GetComponent(uid), _xformQuery);
        var receptions = Filter.Pvs(uid).Recipients;
        foreach (var session in receptions)
        {
            if (!session.AttachedEntity.HasValue)
                continue;

            if (_ignoredRecipients.Contains(session))
                return;

            var xform = _xformQuery.GetComponent(session.AttachedEntity.Value);
            var distance = (sourcePos - _xforms.GetWorldPosition(xform, _xformQuery)).LengthSquared();

            if (distance > WhisperVoiceRange)
                continue;

            var ttsEvent = new PlayTTSEvent(
                soundData,
                GetNetEntity(uid),
                false,
                WhisperVoiceVolumeModifier * (1f - distance / WhisperVoiceRange));
            RaiseNetworkEvent(ttsEvent, session);
        }
    }

    private async void HandleRadio(EntityUid[] uids, string message, TTSVoicePrototype voicePrototype, string channelId, string? effect = null)
    {
        var soundData = await GenerateTTS(message, voicePrototype, _radioEffect);
        if (soundData is null)
            return;

        foreach (var receiver in uids)
        {
            if (!TryComp<ActorComponent>(receiver, out var actor))
                continue;

            if (_ignoredRecipients.Contains(actor.PlayerSession))
                continue;

            float volume = 1.0f;
            if (TryComp<WearingHeadsetComponent>(receiver, out var wearing) && TryComp<HeadsetComponent>(wearing.Headset, out var headset))
            {
                volume = headset.ChannelVolumes.GetValueOrDefault(channelId, 1.0f);
            }

            if (volume <= 0f)
                continue;

            RaiseNetworkEvent(new PlayTTSEvent(soundData, null, true, volume), actor.PlayerSession);
        }
    }

    // ReSharper disable once InconsistentNaming
    public async Task<byte[]?> GenerateTTS(string text, TTSVoicePrototype voicePrototype, string? effect = null)
    {
        try
        {
            var textSanitized = Sanitize(text);
            if (textSanitized == "") return null;
            if (char.IsLetter(textSanitized[^1]))
                textSanitized += ".";

            return await _ttsManager.ConvertTextToSpeech(voicePrototype, textSanitized, effect);
        }
        catch (Exception e)
        {
            // Catch TTS exceptions to prevent a server crash.
            Logger.Error($"TTS System error: {e.Message}");
        }

        return null;
    }
}

public sealed class TransformSpeakerVoiceEvent : EntityEventArgs
{
    public EntityUid Sender;
    public ProtoId<TTSVoicePrototype> VoiceId;
    public string? Effect;

    public TransformSpeakerVoiceEvent(EntityUid sender, ProtoId<TTSVoicePrototype> voiceId, string? effect = null)
    {
        Sender = sender;
        VoiceId = voiceId;
        Effect = effect;
    }
}

public sealed class TTSSanitizeEvent : EntityEventArgs
{
    public string Text;

    public TTSSanitizeEvent(string text)
    {
        Text = text;
    }
}

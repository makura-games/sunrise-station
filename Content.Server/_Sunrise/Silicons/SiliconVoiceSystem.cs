using System.Linq;
using Content.Server._Sunrise.TTS;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Popups;
using Content.Sunrise.Interfaces.Shared;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Silicons.Borgs;

/// <summary>
/// System that handles cyborg voice changing functionality.
/// </summary>
public sealed partial class SiliconVoiceSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private ISharedSponsorsManager? _sponsorsManager;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgVoiceComponent, MapInitEvent>(OnBorgVoiceStartup);

        SubscribeLocalEvent<BorgVoiceComponent, SiliconVoiceChangeActionEvent>(OnBorgVoiceChangeAction);

        // Subscribe to TTS voice transformation
        SubscribeLocalEvent<BorgVoiceComponent, TransformSpeakerVoiceEvent>(OnTransformSpeakerVoice);

        // Initialize sponsors manager
        IoCManager.Instance!.TryResolveType(out _sponsorsManager);

        // Subscribe to UI events
        Subs.BuiEvents<BorgVoiceComponent>(BorgVoiceUiKey.Key, subs =>
        {
            subs.Event<BorgVoiceChangeMessage>(OnBorgVoiceChangeMessage);
        });
    }

    private void OnBorgVoiceChangeAction(EntityUid uid, BorgVoiceComponent component, SiliconVoiceChangeActionEvent args)
    {
        // Open the voice selection UI
        if (!_uiSystem.HasUi(uid, BorgVoiceUiKey.Key))
            return;

        // Get the player session for the performer
        if (!_playerManager.TryGetSessionByEntity(args.Performer, out var session))
            return;

        var state = CreateVoiceChangeState(uid, component, session);
        _uiSystem.SetUiState(uid, BorgVoiceUiKey.Key, state);
        _uiSystem.OpenUi(uid, BorgVoiceUiKey.Key, session);
    }

    private void OnBorgVoiceChangeMessage(EntityUid uid, BorgVoiceComponent component, BorgVoiceChangeMessage args)
    {
        // Get the player session for the actor
        if (!_playerManager.TryGetSessionByEntity(args.Actor, out var session))
            return;

        // Validate the voice prototype exists and player can use it
        if (!CanUseVoice(uid, component, args.VoiceId, session))
        {
            if (!_prototypeManager.TryIndex<TTSVoicePrototype>(args.VoiceId, out var voicePrototype))
            {
                _popup.PopupEntity(Loc.GetString("borg-voice-popup-invalid"), uid, args.Actor, PopupType.MediumCaution);
                return;
            }

            if (voicePrototype.SponsorOnly)
            {
                _popup.PopupEntity(Loc.GetString("borg-voice-popup-sponsor-only"), uid, args.Actor, PopupType.MediumCaution);
                return;
            }
        }

        // Get the voice prototype for the success message
        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(args.VoiceId, out var voice))
            return;

        // Set the new voice
        component.SelectedVoiceId = args.VoiceId;
        Dirty(uid, component);

        _popup.PopupEntity(Loc.GetString("borg-voice-popup-changed", ("voice", Loc.GetString(voice.Name))), uid, args.Actor, PopupType.Medium);

        // Update UI
        var state = CreateVoiceChangeState(uid, component, session);
        _uiSystem.SetUiState(uid, BorgVoiceUiKey.Key, state);
    }

    private void OnBorgVoiceStartup(EntityUid uid, BorgVoiceComponent component, ref MapInitEvent args)
    {
        if (component.SelectedVoiceId != null)
            return;

        var availableVoices = _prototypeManager
            .EnumeratePrototypes<TTSVoicePrototype>().Where(v => v.RoundStart && !v.SponsorOnly && CanUseVoice(uid, component, v.ID, null!)).ToList();

        if (availableVoices.Any())
        {
            component.SelectedVoiceId = availableVoices.First().ID;
            Dirty(uid, component);
        }
    }

    private void OnTransformSpeakerVoice(EntityUid uid, BorgVoiceComponent component, TransformSpeakerVoiceEvent args)
    {
        // Use the borg's selected voice instead of the default
        if (component.SelectedVoiceId != null)
        {
            args.VoiceId = component.SelectedVoiceId.Value;
        }
        args.Effect = component.VoiceEffect;
    }

    private BorgVoiceChangeState CreateVoiceChangeState(EntityUid uid, BorgVoiceComponent component, ICommonSession player)
    {
        var availableVoices = _prototypeManager
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(v => v.RoundStart && CanUseVoice(uid, component, v.ID, player))
            .Select(v => v.ID)
            .ToList();

        return new BorgVoiceChangeState(component.SelectedVoiceId, availableVoices);
    }

    private bool CanUseVoice(EntityUid uid, BorgVoiceComponent component, string voiceId, ICommonSession player)
    {
        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var voice))
            return false;

        if (voice.SponsorOnly)
        {
            if (_sponsorsManager == null)
                return false;
            if (!_sponsorsManager.TryGetPrototypes(player.UserId, out var allowed) || !allowed.Contains(voiceId))
                return false;
        }

        if (component.VoiceWhitelist != null && component.VoiceWhitelist.Any())
            return component.VoiceWhitelist.Contains(voiceId);

        if (component.VoiceBlacklist != null && component.VoiceBlacklist.Any())
            return !component.VoiceBlacklist.Contains(voiceId);

        return true;
    }
}

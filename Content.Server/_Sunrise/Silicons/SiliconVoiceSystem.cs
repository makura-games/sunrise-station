using System.Linq;
using Content.Server.Actions;
using Content.Server._Sunrise.TTS;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
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
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private const string BorgVoiceChangeActionId = "ActionChangeBorgVoice";
    private static readonly EntProtoId BorgVoiceChangeAction = BorgVoiceChangeActionId;

    private ISharedSponsorsManager? _sponsorsManager;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgVoiceComponent, MapInitEvent>(OnBorgVoiceStartup, after: [typeof(ActionGrantSystem)]);

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
        if (!component.VoiceChangeEnabled)
            return;

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
        if (!component.VoiceChangeEnabled)
            return;

        // Get the player session for the actor
        if (!_playerManager.TryGetSessionByEntity(args.Actor, out var session))
            return;

        // Validate the voice prototype exists and player can use it
        if (!CanUseVoice(uid, component, args.VoiceId, session))
        {
            if (!ProtoMan.TryIndex<TTSVoicePrototype>(args.VoiceId, out var voicePrototype))
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
        if (!ProtoMan.TryIndex<TTSVoicePrototype>(args.VoiceId, out var voice))
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
        var dirty = false;

        if (component.SelectedVoiceId == null)
        {
            var availableVoices = ProtoMan
                .EnumeratePrototypes<TTSVoicePrototype>().Where(v => v.RoundStart && !v.SponsorOnly && CanUseVoice(uid, component, v.ID, null!)).ToList();

            if (availableVoices.Any())
            {
                component.SelectedVoiceId = availableVoices.First().ID;
                dirty = true;
            }
        }

        if (!component.VoiceChangeEnabled)
        {
            RevokeVoiceChangeAction((uid, component));
            dirty = true;
        }

        if (dirty)
            Dirty(uid, component);
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

    public void SetVoiceChangeEnabled(EntityUid uid, bool enabled)
    {
        if (!TryComp<BorgVoiceComponent>(uid, out var component))
            return;

        if (TerminatingOrDeleted(uid))
            return;

        component.VoiceChangeEnabled = enabled;

        if (enabled)
            GrantVoiceChangeAction((uid, component));
        else
            RevokeVoiceChangeAction((uid, component));

        Dirty(uid, component);
    }

    private BorgVoiceChangeState CreateVoiceChangeState(EntityUid uid, BorgVoiceComponent component, ICommonSession player)
    {
        var availableVoices = ProtoMan
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(v => v.RoundStart && CanUseVoice(uid, component, v.ID, player))
            .Select(v => v.ID)
            .ToList();

        return new BorgVoiceChangeState(component.SelectedVoiceId, availableVoices);
    }

    private bool CanUseVoice(EntityUid uid, BorgVoiceComponent component, string voiceId, ICommonSession player)
    {
        if (!ProtoMan.TryIndex<TTSVoicePrototype>(voiceId, out var voice))
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

    private void RevokeVoiceChangeAction(Entity<BorgVoiceComponent> borg)
    {
        if (!TryComp<ActionsComponent>(borg, out var actions))
            return;

        foreach (var action in _actions.GetActions(borg, actions).ToArray())
        {
            if (Prototype(action)?.ID != BorgVoiceChangeActionId)
                continue;

            _actions.RemoveAction((borg.Owner, actions), action.AsNullable());
            borg.Comp.VoiceChangeActionEntity = null;
            return;
        }
    }

    private void GrantVoiceChangeAction(Entity<BorgVoiceComponent> borg)
    {
        _actions.AddAction(borg, ref borg.Comp.VoiceChangeActionEntity, BorgVoiceChangeAction);
    }
}

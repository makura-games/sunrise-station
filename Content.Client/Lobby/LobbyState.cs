using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Content.Client._Sunrise;
using Content.Client._Sunrise.Contributors;
using Content.Client._Sunrise.Latejoin;
using Content.Client._Sunrise.ServersHub;
using Content.Client.Audio;
using Content.Client.GameTicking.Managers;
using Content.Client.Lobby.UI;
using Content.Client.Message;
using Content.Client.Playtime;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Voting;
using Content.Shared.CCVar;
using Content.Shared._Sunrise.Contributors;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Numerics;
using System.Threading.Tasks;
using Content.Client._Sunrise.Lobby.UI;
using Content.Client._Sunrise.Options.UI.Tabs;
using Content.Client.Changelog;
using Content.Client.Parallax.Managers;
using Content.Shared._Sunrise.Lobby;
using Content.Shared._Sunrise.ServersHub;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.GameTicking;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Random;
using Robust.Shared.Input;
using Content.Shared.GameTicking.Prototypes;
using ClientRsi = Robust.Client.Graphics.RSI;

namespace Content.Client.Lobby
{
    // TODO: Полностью скопировать в папку санрайза, это сбросить до состояния оффов или закоментировать
    public sealed partial class LobbyState : Robust.Client.State.State
    {
        [Dependency] private IClientConsoleHost _consoleHost = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IResourceCache _resourceCache = default!;
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private IVoteManager _voteManager = default!;
        [Dependency] private ClientsidePlaytimeTrackingManager _playtimeTracking = default!;
        [Dependency] private IPrototypeManager _protoMan = default!;
        [Dependency] private IParallaxManager _parallaxManager = default!;
        [Dependency] private ISerializationManager _serialization = default!;
        [Dependency] private IResourceManager _resource = default!;
        [Dependency] private ServersHubManager _serversHubManager = default!;
        [Dependency] private ContributorsManager _contributorsManager = default!;
        [Dependency] private ChangelogManager _changelogManager = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private NetTexturesManager _netTexturesManager = default!;
        [Dependency] private ILogManager _logManager = default!;
        [Dependency] private IRobustRandom _random = default!;

        private ClientGameTicker _gameTicker = default!;
        private ContentAudioSystem _contentAudioSystem = default!;
        private ISawmill _sawmill = default!;

        private NetTexturesManager.NetTextureAnimationState? _currentAnimationState;
        private ClientRsi.State? _currentLocalAnimationState;
        private int _currentAnimationFrame;
        private float _currentAnimationFrameTime;
        private LobbyBackgroundType? _transientLobbyBackgroundType;
        private string? _transientLobbyArtId;
        private string? _transientLobbyAnimationId;
        private string? _transientLobbyParallaxId;
        private string? _lastLobbyBackgroundTypeSetting;
        private string? _lastLobbyArtSetting;
        private string? _lastLobbyAnimationSetting;
        private string? _lastLobbyParallaxSetting;

        private const string LoadingRsiPath = "/Textures/_Sunrise/loading.rsi";
        private const string LoadingState = "loading";

        protected override Type? LinkedScreenType { get; } = typeof(SunriseLobbyGui);
        public SunriseLobbyGui? Lobby;
        private ChangelogManager.Changelog? _combinedLobbyChangelog;
        private bool _lobbyChangelogLoaded;

        protected override void Startup()
        {
            _sawmill = _logManager.GetSawmill("lobby");

            if (_userInterfaceManager.ActiveScreen == null)
            {
                return;
            }

            Lobby = (SunriseLobbyGui) _userInterfaceManager.ActiveScreen;

            // Sunrise edit - ФИКС ХОТКЕЕВ. НЕ ТРОГАТЬ ЕПТА
            // var chatController = _userInterfaceManager.GetUIController<ChatUIController>();

            _gameTicker = _entityManager.System<ClientGameTicker>();
            _contentAudioSystem = _entityManager.System<ContentAudioSystem>();
            _contentAudioSystem.LobbySoundtrackChanged += UpdateLobbySoundtrackInfo;
            Lobby.Chat.Main = true; // Sunrise edit - ФИКС ХОТКЕЕВ. НЕ ТРОГАТЬ ЕПТА

            _voteManager.SetPopupContainer(Lobby.VoteContainer);
            LayoutContainer.SetAnchorPreset(Lobby, LayoutContainer.LayoutPreset.Wide);

            // Sunrise-Start
            //var lobbyNameCvar = _cfg.GetCVar(CCVars.ServerLobbyName);
            //var serverName = _baseClient.GameInfo?.ServerName ?? string.Empty;

            // Lobby.ServerName.Text = string.IsNullOrEmpty(lobbyNameCvar)
            //     ? Loc.GetString("ui-lobby-title", ("serverName", serverName))
            //     : lobbyNameCvar;

            // var width = _cfg.GetCVar(CCVars.ServerLobbyRightPanelWidth);
            // Lobby.RightPanel.SetWidth = width;

            UpdateLobbyUi();

            var lobbyChangelogs = _cfg.GetCVar(SunriseCCVars.LobbyChangelogsList).Split(',');

            var changelogs = new List<ChangelogManager.Changelog>();
            foreach (var lobbyChangelog in lobbyChangelogs)
            {
                var yamlData = _resource.ContentFileReadYaml(new ResPath($"/Changelog/{lobbyChangelog}"));

                var node = yamlData.Documents[0].RootNode.ToDataNodeCast<MappingDataNode>();
                var changelog = _serialization.Read<ChangelogManager.Changelog>(node, notNullableOverride: true);
                changelogs.Add(changelog);
            }
            _combinedLobbyChangelog = _changelogManager.MergeChangelogs(changelogs);
            _lobbyChangelogLoaded = false;
            Lobby.LobbyAnimation.DisplayRect.Stretch = TextureRect.StretchMode.KeepAspectCovered;
            Lobby.LobbyAnimation.DisplayRect.HorizontalExpand = true;
            Lobby.LobbyAnimation.DisplayRect.VerticalExpand = true;

            // Setup loading animation
            Lobby.LoadingAnimation.DisplayRect.Stretch = TextureRect.StretchMode.KeepAspectCentered;
            Lobby.LoadingAnimation.DisplayRect.TextureScale = new Vector2(2.0f, 2.0f);
            Lobby.LoadingAnimation.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(new ResPath(LoadingRsiPath), LoadingState));
            Lobby.LoadingAnimationContainer.Visible = false;

            // Sunrise edit start - не даем ранним startup callback несколько раз перезагружать lobby assets
            _cfg.OnValueChanged(SunriseCCVars.LobbyBackgroundType, OnLobbyBackgroundTypeChanged, false);
            _cfg.OnValueChanged(SunriseCCVars.LobbyArt, OnLobbyArtChanged, false);
            _cfg.OnValueChanged(SunriseCCVars.LobbyAnimation, OnLobbyAnimationChanged, false);
            _cfg.OnValueChanged(SunriseCCVars.LobbyParallax, OnLobbyParallaxChanged, false);
            _cfg.OnValueChanged(SunriseCCVars.LobbyBackgroundPreset, OnLobbyBackgroundPresetChanged, false);
            // Sunrise edit end

            // Subscribe to resource loaded events
            _netTexturesManager.ResourceLoaded += OnNetworkResourceLoaded;
            // Sunrise-End

            Lobby.CharacterPreview.CharacterSetupButton.OnPressed += OnSetupPressed;
            Lobby.ChangelogHider.OnKeyBindUp += OnChangelogHiderKeyBindUp;
            Lobby.ReadyButton.OnPressed += OnReadyPressed;
            Lobby.ReadyButton.OnToggled += OnReadyToggled;

            _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;

            _serversHubManager.ServersDataListChanged += RefreshServersHubHeader;
            _contributorsManager.ContributorsDataListChanged += RefreshContributorsHeader;

            RefreshContributorsHeader(_contributorsManager.ContributorsDataList);

            // Sunrise-Start
            // Explicitly restore lobby background after reconnection
            // This ensures the background is loaded even if CVar events were called before Lobby initialization
            ApplyConfiguredLobbyBackground();
            // Sunrise-End
        }

        protected override void Shutdown()
        {
            if (Lobby != null)
                Lobby.Chat.Main = false;

            _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated -= LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;
            _contentAudioSystem.LobbySoundtrackChanged -= UpdateLobbySoundtrackInfo;

            _voteManager.ClearPopupContainer();

            Lobby!.CharacterPreview.CharacterSetupButton.OnPressed -= OnSetupPressed;
            Lobby!.ChangelogHider.OnKeyBindUp -= OnChangelogHiderKeyBindUp;
            Lobby!.ReadyButton.OnPressed -= OnReadyPressed;
            Lobby!.ReadyButton.OnToggled -= OnReadyToggled;

            ClearLobbyAnimationState();
            ClearTransientLobbySelections();
            _combinedLobbyChangelog = null;
            _lobbyChangelogLoaded = false;

            Lobby = null;

            _serversHubManager.ServersDataListChanged -= RefreshServersHubHeader;
            _contributorsManager.ContributorsDataListChanged -= RefreshContributorsHeader;

            // Unsubscribe from resource loaded events
            _netTexturesManager.ResourceLoaded -= OnNetworkResourceLoaded;
            _cfg.UnsubValueChanged(SunriseCCVars.LobbyBackgroundType, OnLobbyBackgroundTypeChanged);
            _cfg.UnsubValueChanged(SunriseCCVars.LobbyArt, OnLobbyArtChanged);
            _cfg.UnsubValueChanged(SunriseCCVars.LobbyAnimation, OnLobbyAnimationChanged);
            _cfg.UnsubValueChanged(SunriseCCVars.LobbyParallax, OnLobbyParallaxChanged);
            _cfg.UnsubValueChanged(SunriseCCVars.LobbyBackgroundPreset, OnLobbyBackgroundPresetChanged);
        }

        private void RefreshServersHubHeader(List<ServerHubEntry> servers)
        {
            var totalPlayers = _serversHubManager.ServersDataList.Sum(server => server.CurrentPlayers);
            var maxPlayers = _serversHubManager.ServersDataList.Sum(server => server.MaxPlayers);
            Lobby!.ServersHubHeaderLabel.Text = Loc.GetString("serverhub-playingnow", ("total", totalPlayers), ("max", maxPlayers)); // Sunrise-Edit
        }

        private void RefreshContributorsHeader(List<ContributorEntry> contributors)
        {
            Lobby!.ContributorsHeaderLabel.Text = Loc.GetString("contributors-header-count", ("count", contributors.Count));
        }

        public void SwitchState(SunriseLobbyGui.LobbyGuiState state)
        {
            // Yeah I hate this but LobbyState contains all the badness for now.
            Lobby?.SwitchState(state);
        }

        private void OnSetupPressed(BaseButton.ButtonEventArgs args)
        {
            SetReady(false);
            Lobby?.SwitchState(SunriseLobbyGui.LobbyGuiState.CharacterSetup);
        }

        private void OnChangelogHiderKeyBindUp(GUIBoundKeyEventArgs args)
        {
            if (args.Function != EngineKeyFunctions.Use)
                return;

            EnsureLobbyChangelogPopulated();
        }

        private void EnsureLobbyChangelogPopulated()
        {
            if (_lobbyChangelogLoaded || _combinedLobbyChangelog == null || Lobby == null)
                return;

            Lobby.LocalChangelogBody.PopulateChangelog(_combinedLobbyChangelog);
            _lobbyChangelogLoaded = true;
        }

        private void OnReadyPressed(BaseButton.ButtonEventArgs args)
        {
            if (!_gameTicker.IsGameStarted)
            {
                return;
            }

            new SRLateJoinGui().OpenCentered(); // Sunrise-Edit
        }

        private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
        {
            SetReady(args.Pressed);
        }

        public override void FrameUpdate(FrameEventArgs e)
        {
            UpdateLobbyAnimationFrame(e.DeltaSeconds);

            if (_gameTicker.IsGameStarted)
            {
                var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-time", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));
                return;
            }

            Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-not-started");
            string text;

            if (_gameTicker.Paused)
            {
                text = Loc.GetString("lobby-state-paused");
            }
            else if (_gameTicker.StartTime < _gameTiming.CurTime)
            {
                Lobby!.StationTime.Text = Loc.GetString("lobby-state-soon");
                return;
            }
            else
            {
                var difference = _gameTicker.StartTime - _gameTiming.CurTime;
                var seconds = difference.TotalSeconds;
                if (seconds < 0)
                {
                    text = Loc.GetString(seconds < -5 ? "lobby-state-right-now-question" : "lobby-state-right-now-confirmation");
                }
                else if (difference.TotalHours >= 1)
                {
                    text = $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}";
                }
                else
                {
                    text = $"{difference.Minutes}:{difference.Seconds:D2}";
                }
            }

            Lobby!.StationTime.Text = Loc.GetString("lobby-state-round-start-countdown-text", ("timeLeft", text));
        }

        private void LobbyStatusUpdated()
        {
            // Sunrise added start - новый server status должен сбрасывать временные fallback-выборы лобби
            ClearTransientLobbySelections();
            // Sunrise added end
            ApplyConfiguredLobbyBackground();
            UpdateLobbyUi();
        }

        private void LobbyLateJoinStatusUpdated()
        {
            Lobby!.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin;
        }

        private void UpdateLobbyUi()
        {
            if (_gameTicker.IsGameStarted)
            {
                Lobby!.ReadyButton.Text = Loc.GetString("lobby-state-ready-button-join-state");
                Lobby!.ReadyButton.ToggleMode = false;
                Lobby!.ReadyButton.Pressed = false;
                Lobby!.ObserveButton.Disabled = false;
                // Lobby!.GhostRolesButton.Disabled = false; // Sunrise-edit
            }
            else
            {
                //Lobby!.StartTime.Text = string.Empty;
                Lobby!.ReadyButton.Pressed = _gameTicker.AreWeReady;
                Lobby!.ReadyButton.Text = Loc.GetString(Lobby!.ReadyButton.Pressed ? "lobby-state-player-status-ready": "lobby-state-player-status-not-ready");
                Lobby!.ReadyButton.ToggleMode = true;
                Lobby!.ReadyButton.Disabled = false;
                Lobby!.ObserveButton.Disabled = true;
                // Lobby!.GhostRolesButton.Disabled = true; // Sunrise-edit
            }

            if (_gameTicker.ServerInfoBlob != null)
            {
                Lobby!.ServerInfo.SetInfoBlob(_gameTicker.ServerInfoBlob);
            }

            var minutesToday = _playtimeTracking.PlaytimeMinutesToday;
            if (minutesToday > 60)
            {
                Lobby!.PlaytimeComment.Visible = true;

                var hoursToday = Math.Round(minutesToday / 60f, 1);

                var chosenString = minutesToday switch
                {
                    < 180 => "lobby-state-playtime-comment-normal",
                    < 360 => "lobby-state-playtime-comment-concerning",
                    < 720 => "lobby-state-playtime-comment-grasstouchless",
                    _ => "lobby-state-playtime-comment-selfdestructive"
                };

                Lobby.PlaytimeComment.SetMarkup(Loc.GetString(chosenString, ("hours", hoursToday)));
            }
            else
                Lobby!.PlaytimeComment.Visible = false;
        }

        private void UpdateLobbySoundtrackInfo(LobbySoundtrackChangedEvent ev)
        {
            if (ev.SoundtrackFilename == null)
            {
                Lobby!.LobbySong.SetMarkup(Loc.GetString("lobby-state-song-no-song-text"));
            }
            else if (
                ev.SoundtrackFilename != null
                && _resourceCache.TryGetResource<AudioResource>(ev.SoundtrackFilename, out var lobbySongResource)
                )
            {
                var lobbyStream = lobbySongResource.AudioStream;

                var title = string.IsNullOrEmpty(lobbyStream.Title)
                    ? Loc.GetString("lobby-state-song-unknown-title")
                    : lobbyStream.Title;

                var artist = string.IsNullOrEmpty(lobbyStream.Artist)
                    ? Loc.GetString("lobby-state-song-unknown-artist")
                    : lobbyStream.Artist;

                var markup = Loc.GetString("lobby-state-song-text",
                    ("songTitle", title),
                    ("songArtist", artist));

                Lobby!.LobbySong.SetMarkup(markup);
            }
        }

        // Sunrise-start

        private void OnLobbyBackgroundTypeChanged(string lobbyBackgroundTypeString)
        {
            if (_lastLobbyBackgroundTypeSetting != null &&
                !string.Equals(_lastLobbyBackgroundTypeSetting, lobbyBackgroundTypeString, StringComparison.Ordinal))
            {
                ClearTransientLobbyBackgroundTypeSelection();
            }

            _lastLobbyBackgroundTypeSetting = lobbyBackgroundTypeString;
            ApplyConfiguredLobbyBackground();
        }

        public void SetLobbyBackgroundType(string lobbyBackgroundString)
        {
            var resolvedBackgroundType = ResolveLobbyBackgroundType(lobbyBackgroundString);
            if (resolvedBackgroundType == null)
                return;

            SetLobbyBackgroundType(resolvedBackgroundType.Value);
        }

        private void SetLobbyBackgroundType(LobbyBackgroundType lobbyBackgroundTypeString)
        {
            // Lobby may be null during reconnection or before initialization
            // This is normal, just return silently - the background will be set when Lobby is initialized
            if (Lobby == null)
            {
                _sawmill.Debug("SetLobbyBackgroundType called before Lobby initialization, skipping");
                return;
            }

            switch (lobbyBackgroundTypeString)
            {
                case LobbyBackgroundType.Parallax:
                    ClearLobbyAnimationState();
                    Lobby!.LobbyAnimation.Visible = false;
                    Lobby!.LobbyArt.Visible = false;
                    Lobby!.ShowParallax = true;
                    // Load parallax background
                    UpdateLobbyParallax();
                    break;
                case LobbyBackgroundType.Art:
                    ClearLobbyAnimationState();
                    Lobby!.LobbyAnimation.Visible = false;
                    Lobby!.LobbyArt.Visible = true;
                    Lobby!.ShowParallax = false;
                    // Load art background
                    UpdateLobbyArt();
                    break;
                case LobbyBackgroundType.Animation:
                    Lobby!.LobbyAnimation.Visible = true;
                    Lobby!.LobbyArt.Visible = false;
                    Lobby!.ShowParallax = false;
                    // Load animation background
                    UpdateLobbyAnimation();
                    break;
            }
        }

        private void OnLobbyArtChanged(string lobbyArt)
        {
            if (_lastLobbyArtSetting != null &&
                !string.Equals(_lastLobbyArtSetting, lobbyArt, StringComparison.Ordinal))
            {
                ClearTransientLobbyPrototypeSelection<LobbyArtPrototype>();
            }

            _lastLobbyArtSetting = lobbyArt;
            UpdateLobbyArt();
        }

        private void OnLobbyAnimationChanged(string lobbyAnimation)
        {
            if (_lastLobbyAnimationSetting != null &&
                !string.Equals(_lastLobbyAnimationSetting, lobbyAnimation, StringComparison.Ordinal))
            {
                ClearTransientLobbyPrototypeSelection<LobbyAnimationPrototype>();
            }

            _lastLobbyAnimationSetting = lobbyAnimation;
            UpdateLobbyAnimation();
        }

        private void OnLobbyParallaxChanged(string lobbyParallax)
        {
            if (_lastLobbyParallaxSetting != null &&
                !string.Equals(_lastLobbyParallaxSetting, lobbyParallax, StringComparison.Ordinal))
            {
                ClearTransientLobbyPrototypeSelection<LobbyParallaxPrototype>();
            }

            _lastLobbyParallaxSetting = lobbyParallax;
            UpdateLobbyParallax();
        }

        private void OnLobbyBackgroundPresetChanged(string presetId)
        {
            ClearTransientLobbySelections();
            ApplyConfiguredLobbyBackground();
        }

        private void SetLobbyAnimation(string lobbyAnimation)
        {
            if (ResolveLobbyBackgroundType(GetConfiguredLobbyBackgroundTypeSetting()) !=
                LobbyBackgroundType.Animation)
            {
                ClearLobbyAnimationState();
                return;
            }

            if (!_protoMan.TryIndex<LobbyAnimationPrototype>(lobbyAnimation, out var lobbyAnimationPrototype))
                return;

            if (Lobby == null)
            {
                _sawmill.Debug("SetLobbyAnimation called before Lobby initialization, skipping");
                return;
            }

            Lobby!.LobbyAnimation.Visible = false;
            ShowLoadingAnimation();
            ClearLobbyAnimationState();

            var rsiPath = lobbyAnimationPrototype.Animation;
            if (!rsiPath.EndsWith(".rsi") && !rsiPath.EndsWith(".rsi/"))
            {
                _sawmill.Warning($"Invalid RSI path format: {rsiPath}. Expected path ending with .rsi");
                HideLoadingAnimation();
                return;
            }

            if (UsesNetworkLobbyResource(rsiPath))
            {
                // Sunrise added start - не запрашиваем анимации лобби через NetTextures до свежего lobby status
                if (!_gameTicker.HasLobbyStatus)
                {
                    HideLoadingAnimation();
                    return;
                }
                // Sunrise added end

                if (!_netTexturesManager.EnsureResource(rsiPath))
                    return;

                if (!_netTexturesManager.TryGetAnimationState(rsiPath, lobbyAnimationPrototype.State, out var state) || state == null)
                {
                    _sawmill.Debug($"Lobby animation state '{lobbyAnimationPrototype.State}' is not ready yet for {rsiPath}");
                    return;
                }

                ApplyLobbyAnimationState(state, lobbyAnimationPrototype.Scale);
                return;
            }

            if (!TryGetLocalLobbyAnimationState(rsiPath, lobbyAnimationPrototype.State, out var localState))
            {
                HideLoadingAnimation();
                return;
            }

            ApplyLobbyAnimationState(localState, lobbyAnimationPrototype.Scale);
        }

        private void SetLobbyArt(string lobbyArt)
        {
            if (ResolveLobbyBackgroundType(GetConfiguredLobbyBackgroundTypeSetting()) !=
                LobbyBackgroundType.Art)
            {
                return;
            }

            if (!_protoMan.TryIndex<LobbyArtPrototype>(lobbyArt, out var lobbyArtPrototype))
                return;

            if (Lobby == null)
            {
                _sawmill.Debug("SetLobbyArt called before Lobby initialization, skipping");
                return;
            }

            Lobby!.LobbyArt.Visible = false;
            ShowLoadingAnimation();

            var imagePath = lobbyArtPrototype.Background;

            if (UsesNetworkLobbyResource(imagePath))
            {
                if (!_netTexturesManager.EnsureResource(imagePath))
                    return;

                if (!_netTexturesManager.TryGetTexture(imagePath, out var texture) || texture == null)
                {
                    _sawmill.Debug($"Lobby art texture is not ready yet for {imagePath}");
                    return;
                }

                Lobby!.LobbyArt.Texture = texture;
                Lobby!.LobbyArt.Visible = true;
                HideLoadingAnimation();
                return;
            }

            if (!TryGetLocalLobbyTexture(imagePath, out var localTexture))
            {
                HideLoadingAnimation();
                return;
            }

            Lobby!.LobbyArt.Texture = localTexture;
            Lobby!.LobbyArt.Visible = true;
            HideLoadingAnimation();
        }

        private void SetLobbyParallax(string lobbyParallax)
        {
            if (ResolveLobbyBackgroundType(GetConfiguredLobbyBackgroundTypeSetting()) !=
                LobbyBackgroundType.Parallax)
            {
                return;
            }

            if (!_protoMan.TryIndex<LobbyParallaxPrototype>(lobbyParallax, out var lobbyParallaxPrototype))
                return;

            // Lobby may be null during reconnection or before initialization
            // This is normal, just return silently - the parallax will be set when Lobby is initialized
            if (Lobby == null)
            {
                _sawmill.Debug("SetLobbyParallax called before Lobby initialization, skipping");
                return;
            }

            // Show loading animation for parallax (it may load network textures)
            ShowLoadingAnimation();

            // Subscribe to resource loaded events to hide loading animation when parallax textures are ready
            void OnParallaxResourceLoaded(string resourcePath)
            {
                // Check if parallax is loaded
                if (_parallaxManager.IsLoaded(lobbyParallaxPrototype.Parallax))
                {
                    _netTexturesManager.ResourceLoaded -= OnParallaxResourceLoaded;
                    HideLoadingAnimation();
                }
            }

            _netTexturesManager.ResourceLoaded += OnParallaxResourceLoaded;

            _parallaxManager.LoadParallaxByName(lobbyParallaxPrototype.Parallax).ContinueWith(task =>
            {
                // Hide loading animation when parallax loading completes
                if (Lobby != null && _parallaxManager.IsLoaded(lobbyParallaxPrototype.Parallax))
                {
                    _netTexturesManager.ResourceLoaded -= OnParallaxResourceLoaded;
                    HideLoadingAnimation();
                }
            });

            Lobby!.LobbyParallax = lobbyParallaxPrototype.Parallax;
        }

        private void ApplyConfiguredLobbyBackground()
        {
            var configuredType = GetConfiguredLobbyBackgroundTypeSetting();
            SetLobbyBackgroundType(configuredType);
        }

        private void UpdateLobbyAnimation()
        {
            var animationSetting = _cfg.GetCVar(SunriseCCVars.LobbyAnimation);
            var resolvedAnimation = ResolveLobbyPrototypeId<LobbyAnimationPrototype>(
                animationSetting,
                _gameTicker.LobbyAnimation,
                "animation");

            if (resolvedAnimation != null)
                SetLobbyAnimation(resolvedAnimation);
        }

        private void UpdateLobbyArt()
        {
            var artSetting = _cfg.GetCVar(SunriseCCVars.LobbyArt);
            var resolvedArt = ResolveLobbyPrototypeId<LobbyArtPrototype>(
                artSetting,
                _gameTicker.LobbyArt,
                "art");

            if (resolvedArt != null)
                SetLobbyArt(resolvedArt);
        }

        private void UpdateLobbyParallax()
        {
            var parallaxSetting = _cfg.GetCVar(SunriseCCVars.LobbyParallax);
            var resolvedParallax = ResolveLobbyPrototypeId<LobbyParallaxPrototype>(
                parallaxSetting,
                _gameTicker.LobbyParallax,
                "parallax");

            if (resolvedParallax != null)
                SetLobbyParallax(resolvedParallax);
        }

        private void OnNetworkResourceLoaded(string resourcePath)
        {
            // Lobby may be null during reconnection or before initialization
            // This is normal, just return silently - resources will be loaded when Lobby is initialized
            if (Lobby == null)
            {
                _sawmill.Debug("OnNetworkResourceLoaded called before Lobby initialization, skipping");
                return;
            }

            ApplyConfiguredLobbyBackground();
        }

        private string GetConfiguredLobbyBackgroundTypeSetting()
        {
            return _lastLobbyBackgroundTypeSetting ?? _cfg.GetCVar(SunriseCCVars.LobbyBackgroundType);
        }

        private LobbyBackgroundType? ResolveLobbyBackgroundType(string configuredType)
        {
            var allowedTypes = GetAllowedLobbyBackgroundTypes();

            if (configuredType == ExtraTab.LobbyBackgroundRandom)
            {
                if (_transientLobbyBackgroundType is { } cachedType &&
                    allowedTypes.Contains(cachedType))
                {
                    return cachedType;
                }

                if (!_gameTicker.HasLobbyStatus)
                    return null;

                var resolvedRandomType = ResolveServerOrRandomLobbyBackgroundType(allowedTypes);
                _transientLobbyBackgroundType = resolvedRandomType;
                return resolvedRandomType;
            }

            if (Enum.TryParse<LobbyBackgroundType>(configuredType, true, out var resolvedType) &&
                allowedTypes.Contains(resolvedType))
            {
                if (!_gameTicker.HasLobbyStatus &&
                    !CanResolveLobbyBackgroundTypeWithoutStatus(resolvedType))
                {
                    return null;
                }

                return resolvedType;
            }

            if (!_gameTicker.HasLobbyStatus)
                return null;

            var fallbackType = ResolveServerOrRandomLobbyBackgroundType(allowedTypes);
            _transientLobbyBackgroundType = fallbackType;
            _sawmill.Debug($"Saved lobby background type '{configuredType}' is invalid or unavailable for the current preset. Using transient fallback '{fallbackType}' for this session.");
            return fallbackType;
        }

        private bool CanResolveLobbyBackgroundTypeWithoutStatus(LobbyBackgroundType backgroundType)
        {
            return backgroundType switch
            {
                LobbyBackgroundType.Animation => ResolveLobbyPrototypeId<LobbyAnimationPrototype>(
                    _cfg.GetCVar(SunriseCCVars.LobbyAnimation),
                    _gameTicker.LobbyAnimation,
                    "animation") != null,
                LobbyBackgroundType.Parallax => ResolveLobbyPrototypeId<LobbyParallaxPrototype>(
                    _cfg.GetCVar(SunriseCCVars.LobbyParallax),
                    _gameTicker.LobbyParallax,
                    "parallax") != null,
                LobbyBackgroundType.Art => ResolveLobbyPrototypeId<LobbyArtPrototype>(
                    _cfg.GetCVar(SunriseCCVars.LobbyArt),
                    _gameTicker.LobbyArt,
                    "art") != null,
                _ => false
            };
        }

        private LobbyBackgroundType ResolveServerOrRandomLobbyBackgroundType(IReadOnlyList<LobbyBackgroundType>? allowedTypes = null)
        {
            allowedTypes ??= GetAllowedLobbyBackgroundTypes();

            if (Enum.TryParse<LobbyBackgroundType>(_gameTicker.LobbyType, true, out var serverType) &&
                allowedTypes.Contains(serverType))
            {
                return serverType;
            }

            if (allowedTypes.Count == 0)
                return LobbyBackgroundType.Parallax;

            return _random.Pick(allowedTypes);
        }

        private string? ResolveLobbyPrototypeId<TPrototype>(
            string configuredId,
            string? serverFallbackId,
            string prototypeKind)
            where TPrototype : class, IPrototype
        {
            var allowedIds = GetAllowedLobbyPrototypeIds<TPrototype>();

            if (configuredId == ExtraTab.LobbyBackgroundRandom)
            {
                if (TryGetTransientLobbyPrototypeSelection<TPrototype>(allowedIds, out var cachedId))
                    return cachedId;

                if (!_gameTicker.HasLobbyStatus)
                    return null;

                var resolvedRandomId = ResolveServerOrRandomLobbyPrototypeId<TPrototype>(serverFallbackId, allowedIds);
                CacheTransientLobbyPrototypeSelection<TPrototype>(resolvedRandomId);
                return resolvedRandomId;
            }

            if (TryResolveLobbyPrototypeId(configuredId, allowedIds, out var resolvedConfiguredId))
                return resolvedConfiguredId;

            if (!_gameTicker.HasLobbyStatus)
                return null;

            var fallbackId = ResolveServerOrRandomLobbyPrototypeId<TPrototype>(serverFallbackId, allowedIds);
            if (fallbackId != null)
            {
                CacheTransientLobbyPrototypeSelection<TPrototype>(fallbackId);
                _sawmill.Debug($"Saved lobby {prototypeKind} '{configuredId}' is invalid or unavailable for the current preset. Using transient fallback '{fallbackId}' for this session.");
                return fallbackId;
            }

            _sawmill.Debug($"Saved lobby {prototypeKind} '{configuredId}' is invalid or unavailable for the current preset and no fallback {prototypeKind} is available.");
            return null;
        }

        // Sunrise added start - временный fallback-кэш лобби для случайного выбора и невалидных сохраненных id
        private void ClearTransientLobbySelections()
        {
            _transientLobbyBackgroundType = null;
            _transientLobbyArtId = null;
            _transientLobbyAnimationId = null;
            _transientLobbyParallaxId = null;
            _lastLobbyBackgroundTypeSetting = null;
            _lastLobbyArtSetting = null;
            _lastLobbyAnimationSetting = null;
            _lastLobbyParallaxSetting = null;
        }

        private void ClearTransientLobbyBackgroundTypeSelection()
        {
            _transientLobbyBackgroundType = null;
        }

        private void ClearTransientLobbyPrototypeSelection<TPrototype>()
            where TPrototype : class, IPrototype
        {
            switch (typeof(TPrototype))
            {
                case var type when type == typeof(LobbyArtPrototype):
                    _transientLobbyArtId = null;
                    break;
                case var type when type == typeof(LobbyAnimationPrototype):
                    _transientLobbyAnimationId = null;
                    break;
                case var type when type == typeof(LobbyParallaxPrototype):
                    _transientLobbyParallaxId = null;
                    break;
            }
        }

        private bool TryGetTransientLobbyPrototypeSelection<TPrototype>(
            HashSet<string> allowedIds,
            [NotNullWhen(true)] out string? resolvedId)
            where TPrototype : class, IPrototype
        {
            var cachedId = typeof(TPrototype) switch
            {
                var type when type == typeof(LobbyArtPrototype) => _transientLobbyArtId,
                var type when type == typeof(LobbyAnimationPrototype) => _transientLobbyAnimationId,
                var type when type == typeof(LobbyParallaxPrototype) => _transientLobbyParallaxId,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(cachedId) || !allowedIds.Contains(cachedId))
            {
                resolvedId = null;
                return false;
            }

            resolvedId = cachedId;
            return true;
        }

        private void CacheTransientLobbyPrototypeSelection<TPrototype>(string? resolvedId)
            where TPrototype : class, IPrototype
        {
            switch (typeof(TPrototype))
            {
                case var type when type == typeof(LobbyArtPrototype):
                    _transientLobbyArtId = resolvedId;
                    break;
                case var type when type == typeof(LobbyAnimationPrototype):
                    _transientLobbyAnimationId = resolvedId;
                    break;
                case var type when type == typeof(LobbyParallaxPrototype):
                    _transientLobbyParallaxId = resolvedId;
                    break;
            }
        }
        // Sunrise added end

        private string? ResolveServerOrRandomLobbyPrototypeId<TPrototype>(string? serverFallbackId, HashSet<string> allowedIds)
            where TPrototype : class, IPrototype
        {
            if (TryResolveLobbyPrototypeId(serverFallbackId, allowedIds, out var resolvedServerFallback))
                return resolvedServerFallback;

            if (allowedIds.Count == 0)
                return null;

            return _random.Pick(allowedIds.ToArray());
        }

        private bool TryResolveLobbyPrototypeId(
            string? candidateId,
            HashSet<string> allowedIds,
            [NotNullWhen(true)] out string? resolvedId)
        {
            resolvedId = null;

            if (string.IsNullOrWhiteSpace(candidateId) || candidateId == ExtraTab.LobbyBackgroundRandom)
                return false;

            if (!allowedIds.Contains(candidateId))
                return false;

            resolvedId = candidateId;
            return true;
        }

        private IReadOnlyList<LobbyBackgroundType> GetAllowedLobbyBackgroundTypes()
        {
            var availableTypes = new List<LobbyBackgroundType>();

            if (GetAllowedLobbyAnimationIds().Count > 0)
                availableTypes.Add(LobbyBackgroundType.Animation);

            if (GetAllowedLobbyParallaxIds().Count > 0)
                availableTypes.Add(LobbyBackgroundType.Parallax);

            if (GetAllowedLobbyArtIds().Count > 0)
                availableTypes.Add(LobbyBackgroundType.Art);

            if (availableTypes.Count > 0)
                return availableTypes;

            var fallbackTypes = new List<LobbyBackgroundType>();

            if (_protoMan.EnumeratePrototypes<LobbyAnimationPrototype>().Any())
                fallbackTypes.Add(LobbyBackgroundType.Animation);

            if (_protoMan.EnumeratePrototypes<LobbyParallaxPrototype>().Any())
                fallbackTypes.Add(LobbyBackgroundType.Parallax);

            if (_protoMan.EnumeratePrototypes<LobbyArtPrototype>().Any())
                fallbackTypes.Add(LobbyBackgroundType.Art);

            return fallbackTypes;
        }

        private HashSet<string> GetAllowedLobbyPrototypeIds<TPrototype>()
            where TPrototype : class, IPrototype
        {
            return typeof(TPrototype) switch
            {
                var type when type == typeof(LobbyArtPrototype) => GetAllowedLobbyArtIds(),
                var type when type == typeof(LobbyAnimationPrototype) => GetAllowedLobbyAnimationIds(),
                var type when type == typeof(LobbyParallaxPrototype) => GetAllowedLobbyParallaxIds(),
                _ => _protoMan.EnumeratePrototypes<TPrototype>().Select(x => x.ID).ToHashSet()
            };
        }

        private HashSet<string> GetAllowedLobbyArtIds()
        {
            var preset = GetCurrentLobbyBackgroundPreset();
            return _protoMan.EnumeratePrototypes<LobbyArtPrototype>()
                .Where(x => preset == null || preset.AllArtsAllowed || preset.WhitelistArts.Contains(x.ID))
                .Select(x => x.ID)
                .ToHashSet();
        }

        private HashSet<string> GetAllowedLobbyAnimationIds()
        {
            var preset = GetCurrentLobbyBackgroundPreset();
            return _protoMan.EnumeratePrototypes<LobbyAnimationPrototype>()
                .Where(x => preset == null || preset.AllAnimationsAllowed || preset.WhitelistAnimations.Contains(x.ID))
                .Select(x => x.ID)
                .ToHashSet();
        }

        private HashSet<string> GetAllowedLobbyParallaxIds()
        {
            var preset = GetCurrentLobbyBackgroundPreset();
            return _protoMan.EnumeratePrototypes<LobbyParallaxPrototype>()
                .Where(x => preset == null || preset.AllParallaxesAllowed || preset.WhitelistParallaxes.Contains(x.ID))
                .Select(x => x.ID)
                .ToHashSet();
        }

        private LobbyBackgroundPresetPrototype? GetCurrentLobbyBackgroundPreset()
        {
            var presetId = _cfg.GetCVar(SunriseCCVars.LobbyBackgroundPreset);
            if (_protoMan.TryIndex<LobbyBackgroundPresetPrototype>(presetId, out var preset))
                return preset;

            return null;
        }

        private void ApplyLobbyAnimationState(NetTexturesManager.NetTextureAnimationState state, Vector2 scale)
        {
            if (Lobby == null)
                return;

            _currentAnimationState = state;
            _currentLocalAnimationState = null;
            _currentAnimationFrame = 0;
            _currentAnimationFrameTime = state.GetDelay(0);

            Lobby.LobbyAnimation.DisplayRect.Texture = state.Frame0;
            Lobby.LobbyAnimation.DisplayRect.TextureScale = scale;
            Lobby.LobbyAnimation.Visible = true;
            HideLoadingAnimation();
        }

        private void ApplyLobbyAnimationState(ClientRsi.State state, Vector2 scale)
        {
            if (Lobby == null)
                return;

            _currentAnimationState = null;
            _currentLocalAnimationState = state;
            _currentAnimationFrame = 0;
            _currentAnimationFrameTime = state.GetDelay(0);

            Lobby.LobbyAnimation.DisplayRect.Texture = state.Frame0;
            Lobby.LobbyAnimation.DisplayRect.TextureScale = scale;
            Lobby.LobbyAnimation.Visible = true;
            HideLoadingAnimation();
        }

        private void UpdateLobbyAnimationFrame(float frameTime)
        {
            if (Lobby == null)
                return;

            var oldFrame = _currentAnimationFrame;
            if (_currentAnimationState != null)
            {
                if (!_currentAnimationState.IsAnimated)
                    return;

                _currentAnimationFrameTime -= frameTime;
                while (_currentAnimationFrameTime <= 0f)
                {
                    _currentAnimationFrame = (_currentAnimationFrame + 1) % _currentAnimationState.FrameCount;
                    _currentAnimationFrameTime += _currentAnimationState.GetDelay(_currentAnimationFrame);
                }

                if (_currentAnimationFrame != oldFrame)
                {
                    Lobby.LobbyAnimation.DisplayRect.Texture =
                        _currentAnimationState.GetFrame(RsiDirection.South, _currentAnimationFrame);
                }

                return;
            }

            if (_currentLocalAnimationState == null || !_currentLocalAnimationState.IsAnimated)
                return;

            _currentAnimationFrameTime -= frameTime;
            while (_currentAnimationFrameTime <= 0f)
            {
                _currentAnimationFrame = (_currentAnimationFrame + 1) % _currentLocalAnimationState.DelayCount;
                _currentAnimationFrameTime += _currentLocalAnimationState.GetDelay(_currentAnimationFrame);
            }

            if (_currentAnimationFrame != oldFrame)
            {
                Lobby.LobbyAnimation.DisplayRect.Texture =
                    _currentLocalAnimationState.GetFrame(RsiDirection.South, _currentAnimationFrame);
            }
        }

        private void ClearLobbyAnimationState()
        {
            _currentAnimationState = null;
            _currentLocalAnimationState = null;
            _currentAnimationFrame = 0;
            _currentAnimationFrameTime = 0f;
        }

        private static bool UsesNetworkLobbyResource(string resourcePath)
        {
            return resourcePath.TrimStart('/').StartsWith("NetTextures/", StringComparison.Ordinal);
        }

        private static ResPath GetLocalLobbyResourcePath(string resourcePath)
        {
            if (resourcePath.StartsWith("/", StringComparison.Ordinal))
                return new ResPath(resourcePath).Clean();

            if (resourcePath.StartsWith("Textures/", StringComparison.Ordinal))
                return (ResPath.Root / resourcePath).Clean();

            return (SpriteSpecifierSerializer.TextureRoot / resourcePath).Clean();
        }

        private bool TryGetLocalLobbyTexture(string resourcePath, [NotNullWhen(true)] out Texture? texture)
        {
            var localPath = GetLocalLobbyResourcePath(resourcePath);

            if (_resourceCache.TryGetResource<TextureResource>(localPath, out var textureResource))
            {
                texture = textureResource.Texture;
                return true;
            }

            _sawmill.Warning($"Failed to load local lobby art texture: {localPath}");
            texture = null;
            return false;
        }

        private bool TryGetLocalLobbyAnimationState(string resourcePath, string stateId, [NotNullWhen(true)] out ClientRsi.State? state)
        {
            var localPath = GetLocalLobbyResourcePath(resourcePath);

            if (!_resourceCache.TryGetResource<RSIResource>(localPath, out var rsiResource))
            {
                _sawmill.Warning($"Failed to load local lobby animation RSI: {localPath}");
                state = null;
                return false;
            }

            if (rsiResource.RSI.TryGetState(stateId, out state))
                return true;

            _sawmill.Warning($"Failed to find local lobby animation state '{stateId}' in {localPath}");
            return false;
        }

        /// <summary>
        /// Shows loading animation on the currently visible background element.
        /// </summary>
        private void ShowLoadingAnimation()
        {
            if (Lobby == null)
                return;

            try
            {
                Lobby.LoadingAnimationContainer.Visible = true;
                Lobby.LoadingAnimationContainer.SetPositionLast();
            }
            catch (Exception ex)
            {
                _sawmill.Warning($"Failed to show loading animation: {ex.Message}");
            }
        }

        /// <summary>
        /// Hides loading animation.
        /// </summary>
        private void HideLoadingAnimation()
        {
            if (Lobby != null)
            {
                Lobby.LoadingAnimationContainer.Visible = false;
            }
        }

        // Sunrise-end

        private void SetReady(bool newReady)
        {
            if (_gameTicker.IsGameStarted)
            {
                return;
            }

            _consoleHost.ExecuteCommand($"toggleready {newReady}");
        }
    }
}

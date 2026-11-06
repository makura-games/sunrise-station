using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

using TutorialUIController = Content.Client._Sunrise.Tutorial.TutorialWindow.TutorialUIController;

namespace Content.Client._Sunrise.Lobby.UI;

public sealed partial class SunriseLobbyGui
{
    private bool _cfgHandlersSubscribed;

    #region Buttons

    private void SetupButtonsBinding()
    {
        TutorialButton.OnPressed += _ =>
            UserInterfaceManager.GetUIController<TutorialUIController>().ToggleTutorial();

        ChatHider.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.Use)
                return;

            ChatContent.Visible = !ChatContent.Visible;
            ChatHider.Texture = ChatContent.Visible ? IconExpanded : IconCollapsed;
        };

        ServerInfoHider.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.Use)
                return;

            ServerInfoContent.Visible = !ServerInfoContent.Visible;
            ServerInfoHider.Texture = ServerInfoContent.Visible ? IconExpanded : IconCollapsed;
        };

        CharacterInfoHider.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.Use)
                return;

            CharacterInfoContent.Visible = !CharacterInfoContent.Visible;
            CharacterInfoHider.Texture = CharacterInfoContent.Visible ? IconExpanded : IconCollapsed;
        };

        UserProfileHeader.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.Use)
                return;

            SetUserProfileExpanded(!UserProfileContent.Visible);
        };

        ServersHubHider.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.Use)
                return;

            ServersHubContent.Visible = !ServersHubContent.Visible;
            ServersHubHider.Texture = ServersHubContent.Visible ? IconExpanded : IconCollapsed;
        };

        ContributorsHider.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.Use)
                return;

            ContributorsContent.Visible = !ContributorsContent.Visible;
            ContributorsHider.Texture = ContributorsContent.Visible ? IconExpanded : IconCollapsed;
        };

        ChangelogHider.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.Use)
                return;

            ChangelogContent.Visible = !ChangelogContent.Visible;
            ChangelogHider.Texture = ChangelogContent.Visible ? IconExpanded : IconCollapsed;
        };

        DiscordButton.OnPressed += _ =>
        {
            var url = _cfg.GetCVar(CCVars.InfoLinksDiscord);
            if (!string.IsNullOrEmpty(url))
                _uri.OpenUri(url);
        };

        WikiButton.OnPressed += _ =>
        {
            var url = _cfg.GetCVar(CCVars.InfoLinksWiki);
            if (!string.IsNullOrEmpty(url))
                _uri.OpenUri(url);
        };

        TelegramButton.OnPressed += _ =>
        {
            var url = _cfg.GetCVar(CCVars.InfoLinksTelegram);
            if (!string.IsNullOrEmpty(url))
                _uri.OpenUri(url);
        };

        ReplaysButton.OnPressed += _ =>
        {
            var url = _cfg.GetCVar(SunriseCCVars.InfoLinksReplays);
            if (!string.IsNullOrEmpty(url))
                _uri.OpenUri(url);
        };
    }

    private void SetupButtonsIcons()
    {
        SetupButtonIcon(AHelpButton, "/Textures/Interface/info.svg.192dpi.png", _loc.GetString("ui-lobby-ahelp-button"));
        SetupButtonIcon(MHelpButton, "/Textures/Interface/mentor.svg.192dpi.png", _loc.GetString("ui-lobby-mhelp-button"));
        SetupButtonIcon(CallVoteButton, "/Textures/Interface/gavel.svg.192dpi.png", _loc.GetString("ui-vote-menu-button"));
        SetupButtonIcon(OptionsButton, "/Textures/Interface/VerbIcons/settings.svg.192dpi.png", _loc.GetString("ui-lobby-options-button"));
        SetupButtonIcon(LeaveButton, "/Textures/Interface/VerbIcons/close.svg.192dpi.png", _loc.GetString("ui-lobby-leave-button"));

        SetupButtonIcon(DiscordButton, "/Textures/Interface/discord.svg.192dpi.png", _loc.GetString("server-info-discord-button"));
        SetupButtonIcon(WikiButton, "/Textures/Interface/wiki.svg.192dpi.png", _loc.GetString("server-info-wiki-button"));
        SetupButtonIcon(TelegramButton, "/Textures/Interface/telegram.svg.192dpi.png", _loc.GetString("server-info-telegram-button"));
        SetupButtonIcon(ReplaysButton, "/Textures/Interface/replay.svg.192dpi.png", _loc.GetString("ui-lobby-replays-button"));
    }

    #endregion

    #region CCVars

    protected override void EnteredTree()
    {
        base.EnteredTree();

        SubscribeCfgHandlers();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        UnsubscribeCfgHandlers();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            UnsubscribeCfgHandlers();

        base.Dispose(disposing);
    }

    private void SubscribeCfgHandlers()
    {
        if (_cfgHandlersSubscribed)
            return;

        _cfgHandlersSubscribed = true;

        _cfg.OnValueChanged(SunriseCCVars.LobbyOpacity, OnLobbyOpacityChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.ServersHubEnable, OnServersHubEnableChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.ContributorsEnable, OnContributorsEnableChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.ServerName, OnServerNameChanged, true);

        _cfg.OnValueChanged(CCVars.InfoLinksDiscord, OnDiscordLinkChanged, true);
        _cfg.OnValueChanged(CCVars.InfoLinksWiki, OnWikiLinkChanged, true);
        _cfg.OnValueChanged(CCVars.InfoLinksTelegram, OnTelegramLinkChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.InfoLinksReplays, OnReplaysLinkChanged, true);
    }

    private void UnsubscribeCfgHandlers()
    {
        if (!_cfgHandlersSubscribed)
            return;

        _cfgHandlersSubscribed = false;

        _cfg.UnsubValueChanged(SunriseCCVars.LobbyOpacity, OnLobbyOpacityChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.ServersHubEnable, OnServersHubEnableChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.ContributorsEnable, OnContributorsEnableChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.ServerName, OnServerNameChanged);

        _cfg.UnsubValueChanged(CCVars.InfoLinksDiscord, OnDiscordLinkChanged);
        _cfg.UnsubValueChanged(CCVars.InfoLinksWiki, OnWikiLinkChanged);
        _cfg.UnsubValueChanged(CCVars.InfoLinksTelegram, OnTelegramLinkChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.InfoLinksReplays, OnReplaysLinkChanged);
    }

    #endregion
}

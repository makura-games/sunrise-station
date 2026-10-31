using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.CCVar;


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

        SetupHeaderHoverAndClick(ServersHubHeader, ServersHubHider, () => SetServersHubExpanded(!ServersHubContent.Visible));
        SetupHeaderHoverAndClick(ContributorsHeader, ContributorsHider, () => SetContributorsExpanded(!ContributorsContent.Visible));
        SetupHeaderHoverAndClick(PlaytimeTopHeader, PlaytimeTopHider, () => SetPlaytimeTopExpanded(!PlaytimeTopContent.Visible));
        SetupHeaderHoverAndClick(ChangelogHeader, ChangelogHider, () => SetChangelogExpanded(!ChangelogContent.Visible));
        SetupHeaderHoverAndClick(ServerInfoHeader, ServerInfoHider, () => SetServerInfoExpanded(!ServerInfoContent.Visible));
        SetupHeaderHoverAndClick(CharacterInfoHeader, CharacterInfoHider, () => SetCharacterInfoExpanded(!CharacterInfoContent.Visible));
        SetupHeaderHoverAndClick(ChatHeader, ChatHider, () => SetChatExpanded(!ChatContent.Visible));
        SetupHeaderHoverAndClick(MakuraIDHeader, MakuraIDHider, () => SetMakuraIDExpanded(!MakuraIDContent.Visible));

        DiscordButton.OnPressed += _ =>
        {
            if (!string.IsNullOrEmpty(_cfg.GetCVar(CCVars.InfoLinksDiscord)))
                _uri.OpenUri(_cfg.GetCVar(CCVars.InfoLinksDiscord)!);
        };

        WikiButton.OnPressed += _ =>
        {
            if (!string.IsNullOrEmpty(_cfg.GetCVar(CCVars.InfoLinksWiki)))
                _uri.OpenUri(_cfg.GetCVar(CCVars.InfoLinksWiki)!);
        };

        TelegramButton.OnPressed += _ =>
        {
            if (!string.IsNullOrEmpty(_cfg.GetCVar(CCVars.InfoLinksTelegram)))
                _uri.OpenUri(_cfg.GetCVar(CCVars.InfoLinksTelegram)!);
        };

        GithubButton.OnPressed += _ =>
        {
            if (!string.IsNullOrEmpty(_cfg.GetCVar(CCVars.InfoLinksGithub)))
                _uri.OpenUri(_cfg.GetCVar(CCVars.InfoLinksGithub)!);
        };

        ReplaysButton.OnPressed += _ =>
        {
            if (!string.IsNullOrEmpty(_cfg.GetCVar(SunriseCCVars.InfoLinksReplays)))
                _uri.OpenUri(_cfg.GetCVar(SunriseCCVars.InfoLinksReplays)!);
        };
    }

    private void SetupButtonsIcons()
    {
        SetupButtonIcon(AHelpButton, "/Textures/Interface/info.svg.192dpi.png", _loc.GetString("ui-lobby-ahelp-button"));
        SetupButtonIcon(MHelpButton, "/Textures/Interface/mentor.svg.192dpi.png", _loc.GetString("ui-lobby-mhelp-button"));
        SetupButtonIcon(CallVoteButton, "/Textures/Interface/gavel.svg.192dpi.png", _loc.GetString("ui-vote-menu-button"));
        SetupButtonIcon(OptionsButton, "/Textures/Interface/VerbIcons/settings.svg.192dpi.png", _loc.GetString("ui-lobby-options-button"));
        SetupButtonIcon(LeaveButton, "/Textures/Interface/VerbIcons/close.svg.192dpi.png", _loc.GetString("ui-lobby-leave-button"));

        SetupButtonIcon(DiscordButton, "/Textures/_Sunrise/Interface/discord.svg.192dpi.png", _loc.GetString("server-info-discord-button"));
        SetupButtonIcon(WikiButton, "/Textures/_Sunrise/Interface/wiki.svg.192dpi.png", _loc.GetString("server-info-wiki-button"));
        SetupButtonIcon(TelegramButton, "/Textures/_Sunrise/Interface/telegram.svg.192dpi.png", _loc.GetString("server-info-telegram-button"));
        SetupButtonIcon(GithubButton, "/Textures/_Sunrise/Interface/github.svg.192dpi.png", _loc.GetString("info-link-github"));
        SetupButtonIcon(ReplaysButton, "/Textures/_Sunrise/Interface/replay.svg.192dpi.png", _loc.GetString("ui-lobby-replays-button"));
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
        _cfg.OnValueChanged(CCVars.InfoLinksGithub, OnGithubLinkChanged, true);
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
        _cfg.UnsubValueChanged(CCVars.InfoLinksGithub, OnGithubLinkChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.InfoLinksReplays, OnReplaysLinkChanged);
    }

    #endregion
}



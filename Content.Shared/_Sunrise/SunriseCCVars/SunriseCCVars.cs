using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._Sunrise.SunriseCCVars;

// TODO: Разбиние на partial файлы
// TODO: Документация по каждому из сиваров
[CVarDefs]
public sealed partial class SunriseCCVars : CVars
{
    /**
     * TTS (Text-To-Speech)
     */

    /// <summary>
    /// URL серверного API TTS.
    /// </summary>
    public static readonly CVarDef<bool> TTSEnabled =
        CVarDef.Create("tts.enabled", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// URL серверного API TTS.
    /// </summary>
    public static readonly CVarDef<string> TTSApiUrl =
        CVarDef.Create("tts.api_url", "", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Токен авторизации серверного API TTS.
    /// </summary>
    public static readonly CVarDef<string> TTSApiToken =
        CVarDef.Create("tts.api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Таймаут API в секундах.
    /// </summary>
    public static readonly CVarDef<int> TTSApiTimeout =
        CVarDef.Create("tts.api_timeout", 10, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Настройка для отключения TTS-событий на клиенте.
    /// </summary>
    public static readonly CVarDef<bool> TTSClientEnabled =
        CVarDef.Create("tts.client_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> TTSRadioGhostEnabled =
        CVarDef.Create("tts.radio_ghost_enabled", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Настройка для отключения очереди TTS в радио на клиенте.
    /// </summary>
    public static readonly CVarDef<bool> TTSClientQueueEnabled =
        CVarDef.Create("tts.queue_enabled", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Настройка громкости TTS-звука по умолчанию.
    /// </summary>
    public static readonly CVarDef<float> TTSVolume =
        CVarDef.Create("tts.volume", 0.50f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> TTSRadioVolume =
        CVarDef.Create("tts.radio_volume", 0.50f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> TTSRadioEffect =
        CVarDef.Create("tts.radio_effect", "radio", CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> TTSAnnounceEffect =
        CVarDef.Create("tts.announce_effect", "tiny_room", CVar.SERVERONLY | CVar.ARCHIVE);

    /**
     * Вебхук банов
     */

    public static readonly CVarDef<string> DiscordBanWebhook =
        CVarDef.Create("discord.ban_webhook", string.Empty, CVar.SERVERONLY);

    /*
     * GodMode в конце раунда
     */

    public static readonly CVarDef<bool> GodModeRoundEnd =
        CVarDef.Create("game.godmode_end", false, CVar.SERVERONLY);

    /*
     * Очередь
     */

    public static readonly CVarDef<bool>
        QueueEnabled = CVarDef.Create("queue.enabled", false, CVar.SERVERONLY);

    /*
     *  Sponsor API
     */

    public static readonly CVarDef<bool> SponsorEnabled =
        CVarDef.Create("sponsor.enabled", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> SponsorApiUrl =
        CVarDef.Create("sponsor.api_url", "", CVar.SERVERONLY);

    public static readonly CVarDef<string> SponsorApiToken =
        CVarDef.Create("sponsor.api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> SponsorGhostTheme =
        CVarDef.Create("sponsor.ghost_theme", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> SponsorPet =
        CVarDef.Create("sponsor.pet", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> SponsorProjectName =
        CVarDef.Create("sponsor.project_name", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<int> SponsorMinPlaytimeHours =
        CVarDef.Create("sponsor.min_playtime_hours", 0, CVar.SERVERONLY | CVar.ARCHIVE);

    /*
     *  Приветствия
     */

    public static readonly CVarDef<bool> GreetingsEnable =
        CVarDef.Create("greetings.enable", true);

    public static readonly CVarDef<string> GreetingsMessage =
        CVarDef.Create("greetings.message", "Привет");

    public static readonly CVarDef<string> GreetingsAuthor =
        CVarDef.Create("greetings.author", "Сервер");

    /*
     * New Life
     */

    public static readonly CVarDef<bool> NewLifeEnable =
        CVarDef.Create("newlife.enable", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> NewLifeSponsorOnly =
        CVarDef.Create("newlife.sponsor_only", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> NewLifeTimeout =
        CVarDef.Create("newlife.timeout", 5, CVar.SERVERONLY);

    /*
     * Servers Hub
     */

    public static readonly CVarDef<bool> ServersHubEnable =
        CVarDef.Create("servers_hub.enable", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Список серверов отображаемых в хабе. Разделяются через запятую.
    /// </summary>
    public static readonly CVarDef<string> ServersHubList =
        CVarDef.Create("servers_hub.urls", "", CVar.SERVERONLY);

    /// <summary>
    /// Простое название сервера для отображения в хабе.
    /// </summary>

    public static readonly CVarDef<string> ServersHubShortName =
        CVarDef.Create("servers_hub.short_name", "SS14 SERVER", CVar.SERVERONLY);

    /**
     * Tape Player
     */

    /// <summary>
    /// Параметр отключения школьников с колонками у клиента.
    /// </summary>
    public static readonly CVarDef<bool> TapePlayerClientEnabled =
        CVarDef.Create("tape_player.client_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Информационные ссылки
     */

    /// <summary>
    /// Ссылка на Boosty для показа в лаунчере.
    /// </summary>
    public static readonly CVarDef<string> InfoLinksDonate =
        CVarDef.Create("infolinks.donate", "", CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Ссылка на повторы для показа в меню.
    /// </summary>
    public static readonly CVarDef<string> InfoLinksReplays =
        CVarDef.Create("infolinks.replays", "https://replays.ss14.org", CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> ServerName =
        CVarDef.Create("lobby.server_name", "Stellar Stories", CVar.SERVER | CVar.REPLICATED);

    /*
     * Planet Prison
     */

    public static readonly CVarDef<bool> MinPlayersEnable =
        CVarDef.Create("planet_prison.enable", false, CVar.SERVERONLY);

    public static readonly CVarDef<int> MinPlayersPlanetPrison =
        CVarDef.Create("planet_prison.min_players", 0, CVar.SERVERONLY);

    public static readonly CVarDef<bool> PlanetPrisonModern =
        CVarDef.Create("planet_prison.modern", true, CVar.SERVERONLY);

    /**
     * Roadmap
     */

    public static readonly CVarDef<string> RoadmapId =
        CVarDef.Create("roadmap.id", "SunriseRoadmap");

    /// <summary>
    /// Хеш текста Roadmap, чтобы показывать Roadmap игроку при изменениях.
    /// </summary>
    public static readonly CVarDef<string> RoadmapLastSeenHash =
        CVarDef.Create("roadmap.last_seen_hash", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /**
     * Changelog лобби
     */

    public static readonly CVarDef<string> LobbyChangelogsList =
        CVarDef.Create("lobby_changelog.list", "ChangelogSunrise.yml,Changelog.yml", CVar.SERVER | CVar.REPLICATED);

    /*
     * Криотелепорт
     */

    public static readonly CVarDef<bool> CryoTeleportEnable =
        CVarDef.Create("cryo_teleport.enable", true, CVar.SERVERONLY);

    public static readonly CVarDef<int> CryoTeleportTransferDelay =
        CVarDef.Create("cryo_teleport.transfer_delay", 6, CVar.SERVERONLY);

    /*
     * Damage
     */

    public static readonly CVarDef<float> DamagePositiveVariance =
        CVarDef.Create("damage.positive_variance", 0f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> DamageNegativeVariance =
        CVarDef.Create("damage.negative_variance", 0.3f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> DamageModifier =
        CVarDef.Create("damage.damage_modifier", 1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> HealModifier =
        CVarDef.Create("damage.heal_modifier", 1f, CVar.SERVER | CVar.REPLICATED);

    /*
     * NPCs
     */

    public static readonly CVarDef<bool> NpcDisableWithoutPlayers =
        CVarDef.Create("npc.disable_without_players", true, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> NpcDisableDistance =
        CVarDef.Create("npc.disable_distance", 20f, CVar.SERVERONLY | CVar.ARCHIVE);

    /*
     * Vote
     */

    public static readonly CVarDef<bool> ShowRestartVotes = CVarDef.Create("vote.show_restart_votes", true);

    public static readonly CVarDef<bool> ShowPresetVotes = CVarDef.Create("vote.show_preset_votes", true);

    public static readonly CVarDef<bool> ShowMapVotes = CVarDef.Create("vote.show_map_votes", true);

    /// <summary>
    ///     Makes preset votes ignore player count limits. Intended for local development.
    /// </summary>
    public static readonly CVarDef<bool> IgnorePresetPlayerLimits =
        CVarDef.Create("vote.ignore_preset_player_limits", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> RunMapVoteAfterRestart =
        CVarDef.Create("vote.run_map_vote_after_restart", false);

    public static readonly CVarDef<bool> RunPresetVoteAfterRestart =
        CVarDef.Create("vote.run_preset_vote_after_restart", false);

    public static readonly CVarDef<int> VotingsDelay = CVarDef.Create("vote.votings_delay", 60);

    public static readonly CVarDef<bool> VoteMusicDisable =
        CVarDef.Create("vote.music_disable", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> VoteDisableOOC =
        CVarDef.Create("vote.disable_ooc", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> VotePause =
        CVarDef.Create("vote.pause", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> ExcludeMaps = CVarDef.Create("vote.exclude_maps", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> ExcludePresets =
        CVarDef.Create("vote.exclude_presets", true, CVar.SERVERONLY);

    /*
     * Preset
     */

    public static readonly CVarDef<bool> ResetPresetAfterRestart =
        CVarDef.Create("game.reset_preset_after_restart", false);

    public static readonly CVarDef<string> GamePresetPool =
        CVarDef.Create("game.preset_pool", "DefaultHighPopPresetPool", CVar.SERVERONLY);

    /*
     * Ban links.
     */

    public static readonly CVarDef<string> BanDiscordLink =
        CVarDef.Create("cl.discord_link", "", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<string> BanTelegramLink =
        CVarDef.Create("cl.telegram_link", "", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /*
     * Mood
     */

    public static readonly CVarDef<bool> MoodEnabled =
        CVarDef.Create("mood.enabled", true, CVar.SERVER);

    public static readonly CVarDef<bool> MoodIncreasesSpeed =
        CVarDef.Create("mood.increases_speed", true, CVar.SERVER);

    public static readonly CVarDef<bool> MoodDecreasesSpeed =
        CVarDef.Create("mood.decreases_speed", true, CVar.SERVER);

    /**
     * Jump
     */

    public static readonly CVarDef<bool> JumpEnable =
        CVarDef.Create("jump.enable", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> JumpCooldown =
        CVarDef.Create("jump.cooldown", 0.600f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> JumpSoundDisable =
        CVarDef.Create("jump.sound_disable", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> BunnyHopEnable =
        CVarDef.Create("bunny_hop.enable", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> BunnyHopSpeedUpPerJump =
        CVarDef.Create("bunny_hop.speed_up_per_jump", 0.005f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> BunnyHopSpeedLimit =
        CVarDef.Create("bunny_hop.speed_limit", 2.0f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> BunnyHopMinSpeedThreshold =
        CVarDef.Create("bunny_hop.min_speed_threshold", 4.0f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> BunnyHopSpeedBoostWindow =
        CVarDef.Create("bunny_hop.speed_boost_window", 0.610f, CVar.SERVER | CVar.REPLICATED);

    /**
     * Flip
     */

    public static readonly CVarDef<float> FlipDeadChance =
        CVarDef.Create("flip.dead_chance", 0.001f, CVar.SERVER | CVar.REPLICATED);

    /**
     * Slip
     */

    public static readonly CVarDef<float> SlipDeadChance =
        CVarDef.Create("slip.dead_chance", 0.001f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Список имен пользователей, которых нельзя кикнуть командами kick или ghostkick.
    /// Имена пользователей разделяются запятыми.
    /// </summary>
    public static readonly CVarDef<string> ProtectedKickUsernames =
        CVarDef.Create("admin.protected_kick_usernames", "", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Флейвор-профиль.
    /// </summary>
    public static readonly CVarDef<bool> FlavorTextSponsorOnly =
        CVarDef.Create("flavor_text.sponsor_only", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> FlavorTextBaseLength =
        CVarDef.Create("flavor_text.length", 512, CVar.SERVER | CVar.REPLICATED);

    /*
     * Damage Overlay
     */

    public static readonly CVarDef<bool> DamageOverlayEnable =
        CVarDef.Create("damage_overlay.enable", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> DamageOverlaySelf =
        CVarDef.Create("damage_overlay.self", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> DamageOverlayStructures =
        CVarDef.Create("damage_overlay.structures", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Отключение звука новой ghost role
     */

    public static readonly CVarDef<bool> MuteGhostRoleNotification =
        CVarDef.Create("ghost.mute_role_notification", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Звук сердцебиения
     */

    public static readonly CVarDef<bool> PlayHeartBeatSound =
        CVarDef.Create("heartbeat.play_sound", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Вроде все очевидно
    /// </summary>
    public static readonly CVarDef<string> IpWhitelist =
        CVarDef.Create("admin.ip_whitelist", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<bool> TracesEnabled =
        CVarDef.Create("opt.traces_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> HoldLookUp =
        CVarDef.Create("scope.hold_look_up", true, CVar.CLIENT | CVar.ARCHIVE);

    /// <summary>
    /// Название корпорации для подстановки в документы
    /// </summary>
    public static readonly CVarDef<string> DocumentCorporationName =
        CVarDef.Create("doc.corp_name", "NanoTrasen", CVar.SERVER | CVar.ARCHIVE);

    /// <summary>
    /// Смешение автозаполнения времени в часах
    /// </summary>
    public static readonly CVarDef<int> DocumentTimeOffsetHours =
        CVarDef.Create("doc.time_offset_hours", 3, CVar.SERVER | CVar.ARCHIVE);

    /// <summary>
    /// Смешение автозаполнения времени в годах
    /// </summary>
    public static readonly CVarDef<int> DocumentYearOffset =
        CVarDef.Create("doc.year_offset", 1000, CVar.SERVER | CVar.ARCHIVE);

    /// <summary>
    /// Пул шаблонов документов, используемых при создании документов на принтере
    /// </summary>
    public static readonly CVarDef<string> DocumentTemplatePool =
        CVarDef.Create("doc.template_pool", "Sunrise", CVar.SERVER | CVar.ARCHIVE);

    public static readonly CVarDef<bool> GameIPBlockingEnabled =
        CVarDef.Create("game.ipblocking_enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<int> GameIPBlockingDuration =
        CVarDef.Create("game.ipblocking_duration", 900, CVar.SERVERONLY);

    public static readonly CVarDef<int> GameIPBlockingMaxResponseLength =
        CVarDef.Create("game.ipblocking_max_response_length", 10485760, CVar.SERVERONLY);

    public static readonly CVarDef<int> GameIPBlockingUnhandledMessageRateLimit =
        CVarDef.Create("game.ipblocking_unhandled_message_rate_limit", 10, CVar.SERVERONLY);


    /*
    LOADOUTS
    */

    /// <summary>
    /// Включает кастомный пул loadout
    /// Если выключено, используется стандартный пул из loadout-прототипов
    /// Имя пула задаётся в custom_loadout.pool
    /// </summary>
    public static readonly CVarDef<bool> CustomLoadoutEnabled =
        CVarDef.Create("custom_loadout.enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Имя кастомного пула loadout
    /// Используется только если custom_loadout.enabled = true
    /// Варианты: "SunriseLoadout"
    /// </summary>
    public static readonly CVarDef<string> LoadoutPool =
        CVarDef.Create("custom_loadout.pool", "SunriseLoadout", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /*
     * Доверенный прокси (UDP Relay)
     */

    /// <summary>
    /// Включает поддержку доверенных прокси-серверов (UDP relay).
    /// При включении сервер будет polling relay API для получения маппингов IP-адресов.
    /// </summary>
    public static readonly CVarDef<bool> TrustedProxyEnabled =
        CVarDef.Create("trusted_proxy.enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// URL(s) relay API для polling маппингов, через запятую.
    /// Пример: "http://10.0.0.1:18080" или "http://relay1.example.com:18080,http://relay2.example.com:18080"
    /// </summary>
    public static readonly CVarDef<string> TrustedProxyRelayApiUrl =
        CVarDef.Create("trusted_proxy.relay_api_url", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// API ключ для аутентификации при polling relay API.
    /// </summary>
    public static readonly CVarDef<string> TrustedProxyRelayApiKey =
        CVarDef.Create("trusted_proxy.relay_api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Список IP-адресов доверенных прокси-серверов, через запятую.
    /// Подключения с этих IP будут проходить трансляцию endpoint'ов.
    /// Пример: "1.2.3.4,5.6.7.8"
    /// </summary>
    public static readonly CVarDef<string> TrustedProxyIPs =
        CVarDef.Create("trusted_proxy.ips", "", CVar.SERVERONLY);

    /// <summary>
    /// Интервал polling relay API в миллисекундах.
    /// </summary>
    public static readonly CVarDef<int> TrustedProxyPollIntervalMs =
        CVarDef.Create("trusted_proxy.poll_interval_ms", 2000, CVar.SERVERONLY);

    /// <summary>
    ///     Должен ли каждый поздний игрок прибывать на своем маленьком шаттле.
    ///     Это альтернатива большому rolling arrivals shuttle.
    /// </summary>
    public static readonly CVarDef<bool> ArrivalsSingleShuttle =
        CVarDef.Create("shuttle.arrivals_single_shuttle", true, CVar.SERVERONLY);

    /// <summary>
    ///     Карта для персонального arrivals shuttle.
    /// </summary>
    public static readonly CVarDef<string> ArrivalsSingleShuttlePath =
        CVarDef.Create("shuttle.arrivals_single_shuttle_path", "/Maps/_Sunrise/Shuttles/depart.yml", CVar.SERVERONLY);

    /// <summary>
    ///     Визуальная скорость всех шаттлов на FTL-карте.
    /// </summary>
    public static readonly CVarDef<float> FTLSpeed =
        CVarDef.Create("shuttle.ftl_speed", 100.0f, CVar.SERVERONLY);

    /**
     * Загрузка фотографий
     */

    /// <summary>
    /// Включена ли загрузка изображений в PDA messenger и консоли новостей.
    /// </summary>
    public static readonly CVarDef<bool> PhotoUploadEnabled =
        CVarDef.Create("photo.upload_enabled", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Включена ли съемка фотографий через PDA camera.
    /// </summary>
    public static readonly CVarDef<bool> PhotoCaptureEnabled =
        CVarDef.Create("photo.capture_enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// ID прототипа набора корпоративных законов для PDA-приложения.
    /// </summary>
    public static readonly CVarDef<string> CorporateLawSet =
        CVarDef.Create("sunrise.corporate_law_set", "StandardCorporateLaw", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /*
     * Tutorial
     */
    public static readonly CVarDef<bool> TutorialWindowAutoOpen =
        CVarDef.Create("tutorial.window_auto_open", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> TutorialMaxActive =
        CVarDef.Create("tutorial.max_active", 10, CVar.SERVERONLY);

    public static readonly CVarDef<TimeSpan> TutorialCooldown =
        CVarDef.Create("tutorial.cooldown", TimeSpan.FromSeconds(5), CVar.SERVERONLY);

    /// <summary>
    /// Maximum account age at the first tutorial completion for the player to be counted as a new account.
    /// </summary>
    public static readonly CVarDef<TimeSpan> TutorialNewAccountThreshold =
        CVarDef.Create("tutorial.metrics.new_account_threshold", TimeSpan.FromDays(7), CVar.SERVERONLY);

    public static readonly CVarDef<float> TutorialPromptSkipDelay =
        CVarDef.Create("tutorial.prompt_skip_delay", 15f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<bool> TutorialPromptSeen =
        CVarDef.Create("tutorial.prompt_seen", false, CVar.CLIENTONLY | CVar.ARCHIVE);

}

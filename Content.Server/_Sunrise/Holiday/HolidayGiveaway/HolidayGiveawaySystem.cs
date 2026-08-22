using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Holiday;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Holiday.HolidayGiveaway;

/// <summary>
/// Система для выдачи различных подарков во время определенных праздников.
/// </summary>
public sealed partial class HolidayGiveawaySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private HolidaySystem _holiday = default!;
    [Dependency] private HandsSystem _hands = default!;

    /// <summary>
    /// Кешированные текущие раздачи, которые будут применены после спавна игрока в <see cref="OnPlayerSpawn"/> <br/>
    /// Сбрасываются и пересчитываются каждый раунд в <see cref="CacheGiveaways()"/>
    /// </summary>
    private readonly List<ProtoId<HolidayGiveawayItemPrototype>> _activeGiveaways = [];

    [ViewVariables]
    private bool _enabled = true;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configuration, CCVars.HolidaysEnabled, OnHolidaysEnableChange);

        SubscribeLocalEvent<GameRunLevelChangedEvent>(CacheGiveaways);
        SubscribeLocalEvent<RoundStartedEvent>(CacheGiveaways);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<PreventHolidayGiveawayComponent, HolidayGiveawayAttemptEvent>(Cancel);
    }

    /// <summary>
    /// Основной метод для кеширования подарков.
    /// </summary>
    private void CacheGiveaways(GameRunLevelChangedEvent ev)
    {
        if (!_enabled)
            return;

        if (ev.New != GameRunLevel.PreRoundLobby)
            return;

        CacheGiveaways();
    }

    /// <summary>
    /// Дополнительный метод для кеширования подарков.
    /// Специально для случая, когда лобби недоступно или выключено.
    /// </summary>
    private void CacheGiveaways(RoundStartedEvent ev)
    {
        if (!_enabled)
            return;

        if (_activeGiveaways.Count != 0)
            return;

        CacheGiveaways();
    }

    private void CacheGiveaways()
    {
        _activeGiveaways.Clear();

        foreach (var giveaway in _prototype.EnumeratePrototypes<HolidayGiveawayItemPrototype>())
        {
            if (!_holiday.IsCurrentlyHoliday(giveaway.Holiday))
                continue;

            _activeGiveaways.Add(giveaway.ID);
        }
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!_enabled)
            return;

        if (_activeGiveaways.Count == 0)
            return;

        var attempt = new HolidayGiveawayAttemptEvent();
        RaiseLocalEvent(ev.Mob, ref attempt);

        if (attempt.Canceled)
            return;

        foreach (var giveawayProto in _activeGiveaways)
        {
            var giveAway = _prototype.Index(giveawayProto);
            var present = SpawnNextToOrDrop(giveAway.Prototype, ev.Mob);

            _hands.PickupOrDrop(ev.Mob, present);
        }
    }

    private void Cancel(Entity<PreventHolidayGiveawayComponent> ent, ref HolidayGiveawayAttemptEvent args)
    {
        args.Canceled = true;
    }

    private void OnHolidaysEnableChange(bool enabled)
    {
        _enabled = enabled;

        if (enabled)
            CacheGiveaways();
        else
            _activeGiveaways.Clear();
    }
}

[ByRefEvent]
public record struct HolidayGiveawayAttemptEvent
{
    public bool Canceled;
}

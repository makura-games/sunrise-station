using Content.Server._Sunrise.BloodCult.Items.Components;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.BloodCult.Items.Systems;

public sealed partial class ShuttleCurseSystem : EntitySystem
{
    private const int MaxCurses = 3;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    private int _currentCurses = 0;
    private TimeSpan? _nextCurse = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleCurseComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnd);
    }

    private void OnRoundEnd(RoundEndedEvent ev)
    {
        _currentCurses = 0;
        _nextCurse = TimeSpan.Zero;
    }

    private void OnUse(EntityUid uid, ShuttleCurseComponent component, UseInHandEvent args)
    {
        if (!HasComp<BloodCultistComponent>(args.User))
        {
            _hands.TryDrop(args.User);
            _popup.PopupEntity(Loc.GetString("shuttle-curse-not-cultist"), args.User, args.User);
            return;
        }

        if (!_roundEnd.ShuttleCalled())
        {
            _popup.PopupEntity(Loc.GetString("shuttle-curse-shuttle-not-called"), args.User, args.User);
            return;
        }

        if (_currentCurses >= MaxCurses)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-curse-max-curses"), args.User, args.User);
            return;
        }

        if (_nextCurse > _gameTiming.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-curse-cooldown"), args.User, args.User);
            return;
        }

        var shuttle = _entMan.System<EmergencyShuttleSystem>();

        if (shuttle.EmergencyShuttleArrived)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-curse-shuttle-arrived"), args.User, args.User);
            return;
        }

        _roundEnd.DelayCursedShuttle(component.DelayTime);
        _popup.PopupEntity(Loc.GetString("shuttle-curse-shuttle-delayed"), args.User, args.User);

        _currentCurses++;
        _nextCurse = _gameTiming.CurTime + component.Cooldown;
    }
}

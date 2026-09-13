using System.Linq;
using System.Numerics;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.AssaultOps.Icarus;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Station.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.AssaultOps.Icarus;

/// <summary>
/// Handle Icarus activation terminal
/// </summary>
public sealed partial class IcarusTerminalSystem : EntitySystem
{
    private const string IcarusBeamPrototypeId = "IcarusBeam";

    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private IcarusBeamSystem _icarusSystem = default!;
    [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private RoundEndSystem _roundEndSystem = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IcarusTerminalComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IcarusTerminalComponent, EntInsertedIntoContainerMessage>(OnItemSlotInserted);
        SubscribeLocalEvent<IcarusTerminalComponent, EntRemovedFromContainerMessage>(OnItemSlotRemoved);

        // UI events
        SubscribeLocalEvent<IcarusTerminalComponent, IcarusTerminalFireMessage>(OnFireButtonPressed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQuery<IcarusTerminalComponent>();
        foreach (var terminal in query)
        {
            switch (terminal.Status)
            {
                case IcarusTerminalStatus.FIRE_PREPARING:
                    TickTimer(terminal, frameTime);
                    break;
                case IcarusTerminalStatus.COOLDOWN:
                    TickCooldown(terminal, frameTime);
                    break;
            }

            if (terminal.Status != IcarusTerminalStatus.AWAIT_DISKS)
            {
                TickTimerEndRound(terminal, frameTime);
            }
        }
    }

    private void TickTimerEndRound(IcarusTerminalComponent component, float frameTime)
    {
        component.TimerRoundEnd -= frameTime;
        if (!(component.TimerRoundEnd <= 0))
            return;
        component.TimerRoundEnd = 0;
        _roundEndSystem.EndRound();
    }

    private void OnMapInit(EntityUid uid, IcarusTerminalComponent component, MapInitEvent args)
    {
        _itemSlotsSystem.AddItemSlot(uid, IcarusTerminalComponent.FirstKeySlotId, component.FirstKeySlot);
        _itemSlotsSystem.AddItemSlot(uid, IcarusTerminalComponent.SecondKeySlotId, component.SecondKeySlot);
        _itemSlotsSystem.AddItemSlot(uid, IcarusTerminalComponent.ThirdKeySlotId, component.ThirdKeySlot);
        component.RemainingTime = component.Timer;
        UpdateStatus(component);
        UpdateUserInterface(component);
    }

    private void OnItemSlotInserted(EntityUid uid, IcarusTerminalComponent component, ContainerModifiedMessage args)
    {
        OnItemSlotChanged(component);
    }

    private void OnItemSlotRemoved(EntityUid uid, IcarusTerminalComponent component, ContainerModifiedMessage args)
    {
        OnItemSlotChanged(component);
    }

    private void OnItemSlotChanged(IcarusTerminalComponent component)
    {
        UpdateStatus(component);
        UpdateUserInterface(component);
    }

    private void OnFireButtonPressed(EntityUid uid, IcarusTerminalComponent component, IcarusTerminalFireMessage args)
    {
        Fire(component);
    }

    private void Fire(IcarusTerminalComponent component)
    {
        if (component.Status == IcarusTerminalStatus.FIRE_PREPARING)
            return;

        component.RemainingTime = component.Timer;
        component.Status = IcarusTerminalStatus.FIRE_PREPARING;

        var stationName = "/NTSS14/";

        var targetStation = _stationSystem.GetStations().FirstOrNull();

        if (targetStation != null)
        {
            stationName = Name(targetStation.Value);
        }

        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString("icarus-fire-announcement", ("seconds", component.Timer),
                ("station", stationName)),
            Loc.GetString("icarus-announce-sender"),
            false,
            colorOverride: Color.Red);
        _audio.PlayGlobal(component.AlertSound, Filter.Broadcast(), false);
    }

    private void UpdateStatus(IcarusTerminalComponent component)
    {
        switch (component.Status)
        {
            case IcarusTerminalStatus.AWAIT_DISKS:
                if (IsAccessGranted(component.Owner))
                    Authorize(component);
                break;
            case IcarusTerminalStatus.FIRE_READY:
            {
                if (!IsAccessGranted(component.Owner))
                {
                    component.Status = IcarusTerminalStatus.AWAIT_DISKS;
                }
                break;
            }
        }
    }

    private void UpdateUserInterface(IcarusTerminalComponent component)
    {
        _userInterfaceSystem.SetUiState(component.Owner, IcarusTerminalUiKey.Key, new IcarusTerminalUiState(
            component.Status,
            (int) component.RemainingTime,
            (int) component.CooldownTime)
        );
    }

    private bool IsAccessGranted(EntityUid uid)
    {
        return TryComp<ItemSlotsComponent>(uid, out var itemSlotsComponent) && itemSlotsComponent.Slots.Values.All(v => v.HasItem);
    }

    private void Authorize(IcarusTerminalComponent component)
    {
        component.Status = IcarusTerminalStatus.FIRE_READY;

        if (!component.AuthorizationNotified)
        {
            _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("icarus-authorized-announcement"),
                Loc.GetString("icarus-announce-sender"),
                false,
                component.ActiveGoldenEyeAlertSound);
            component.AuthorizationNotified = true;

            RaiseLocalEvent(new IcarusActivatedEvent()
            {
                OwningStation = Transform(component.Owner).GridUid,
            });
        }
    }

    private void TickCooldown(IcarusTerminalComponent component, float frameTime)
    {
        component.CooldownTime -= frameTime;
        if (component.CooldownTime <= 0)
        {
            component.CooldownTime = 0;
            component.Status = IcarusTerminalStatus.AWAIT_DISKS;
            UpdateStatus(component);
        }

        UpdateUserInterface(component);
    }

    private void TickTimer(IcarusTerminalComponent component, float frameTime)
    {
        component.RemainingTime -= frameTime;
        if (component.RemainingTime <= 0)
        {
            component.RemainingTime = 0;
            ActivateBeamOnStation(component);
        }

        UpdateUserInterface(component);
    }

    private void ActivateBeamOnStation(IcarusTerminalComponent component)
    {
        component.Status = IcarusTerminalStatus.COOLDOWN;
        component.CooldownTime = component.Cooldown;

        _audio.PlayGlobal(component.FireSound, Filter.Broadcast(), false);
        FireBeam(GetStationArea());

        // Ждём 10 секунд, прежде чем активировать дельту и эвакуироваться
        Timer.Spawn(TimeSpan.FromSeconds(10), () =>
        {
            var targetStation = _stationSystem.GetStations().FirstOrNull();
            if (targetStation != null)
            {
                _alertLevel.SetLevel(targetStation.Value, "delta", true, true, true);
            }

            _roundEndSystem.ForceSetCountdown(TimeSpan.FromMinutes(1), cantRecall: true);
        });
    }

    public MapCoordinates FireBeam(Box2 area)
    {
        TryGetBeamSpawnLocation(area, out var coords, out var offset);
        Logger.DebugS("icarus", $"Try spawn beam on coords: {coords.ToString()}");
        var entUid = Spawn(IcarusBeamPrototypeId, coords);
        _icarusSystem.LaunchInDirection(entUid, -offset.Normalized());
        return coords;
    }

    private void TryGetBeamSpawnLocation(Box2 area, out MapCoordinates coords,
        out Vector2 offset)
    {
        coords = MapCoordinates.Nullspace;
        offset = Vector2.Zero;

        var center = area.Center;
        var distance = (area.TopRight - center).Length();
        var angle = new Angle(_robustRandom.NextFloat() * MathF.Tau);

        offset = angle.RotateVec(new Vector2(distance + 50f, 0));
        coords = new MapCoordinates(center + offset, _gameTicker.DefaultMap);
    }

    /// <summary>
    ///     Determine box of all stations and all of they grids. (copy-paste from pirate gamerule)
    /// </summary>
    /// <returns>Box of all station grids</returns>
    private Box2 GetStationArea()
    {
        var areas = _stationSystem.GetStations().SelectMany(s =>
            Comp<StationDataComponent>(s).Grids.Select(g =>
                _xformQuery.GetComponent(g).WorldMatrix.TransformBox(Comp<MapGridComponent>(g).LocalAABB))).ToArray();

        var stationArea = areas[0];
        for (var i = 1; i < areas.Length; i++)
            stationArea.Union(areas[i]);

        return stationArea;
    }


    public sealed class IcarusActivatedEvent : EntityEventArgs
    {
        public EntityUid? OwningStation;
    }
}

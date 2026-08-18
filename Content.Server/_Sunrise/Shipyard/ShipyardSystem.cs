using System.Diagnostics.CodeAnalysis;

using Content.Server.Cargo.Systems;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared._Sunrise.Shipyard;
using Content.Shared._Sunrise.Shipyard.Components;
using Content.Shared._Sunrise.Shipyard.Events;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Content.Shared.Repairable;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Shipyard;

/// <summary>
/// Handles shipyard console transactions and shuttle ownership.
/// </summary>
public sealed partial class ShipyardSystem : EntitySystem
{
    private const string InvalidVesselMessage = "shipyard-console-invalid-vessel";

    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCargoSystem _cargo = default!;
    [Dependency] private readonly DockingSystem _docking = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly List<PendingShipyardAction> _pendingActions = new();
    private readonly List<PendingShipyardPurchase> _pendingPurchases = new();
    private EntityQuery<ShipyardConsoleComponent> _consoleQuery;

    public override void Initialize()
    {
        base.Initialize();

        _consoleQuery = GetEntityQuery<ShipyardConsoleComponent>();

        Subs.BuiEvents<ShipyardConsoleComponent>(ShipyardConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<ShipyardConsolePurchaseMessage>(OnPurchase);
            subs.Event<ShipyardConsoleSellMessage>(OnSell);
        });

        SubscribeLocalEvent<ShipyardConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ShipyardConsoleComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<ShipyardConsoleComponent, BreakageEventArgs>(OnBreakage);
        SubscribeLocalEvent<ShipyardConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<ShipyardConsoleComponent, RepairedEvent>(OnRepaired);
        SubscribeLocalEvent<StationBankAccountComponent, BankBalanceUpdatedEvent>(OnBankBalanceUpdated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdatePendingPurchases();

        for (var i = _pendingActions.Count - 1; i >= 0; i--)
        {
            var action = _pendingActions[i];
            if (_timing.CurTime < action.CompleteAt)
                continue;

            _pendingActions.RemoveAt(i);
            if (!_consoleQuery.TryComp(action.Console, out var console))
                continue;

            CompleteSale((action.Console, console), action);
        }
    }

    private void OnConsoleShutdown(Entity<ShipyardConsoleComponent> ent, ref ComponentShutdown args)
    {
        for (var i = _pendingPurchases.Count - 1; i >= 0; i--)
        {
            var purchase = _pendingPurchases[i];
            if (purchase.Console != ent.Owner)
                continue;

            _pendingPurchases.RemoveAt(i);
            RefundPendingPurchase(purchase);
            CleanupPendingPurchase(purchase);
        }

        for (var i = _pendingActions.Count - 1; i >= 0; i--)
        {
            if (_pendingActions[i].Console == ent.Owner)
                _pendingActions.RemoveAt(i);
        }
    }

    private void OnInteractUsing(Entity<ShipyardConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CashComponent>(args.Used))
            return;

        args.Handled = TryDepositCash(
            (ent.Owner, (ShipyardConsoleComponent?) ent.Comp),
            args.Used,
            args.User);
    }

    public bool TryDepositCash(Entity<ShipyardConsoleComponent?> ent, EntityUid cash, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        Entity<ShipyardConsoleComponent> resolved = (ent.Owner, ent.Comp);

        if (!CanDepositCash(ent, cash, user, out var reason))
        {
            if (reason is not null)
                Deny(resolved, user, reason);

            return false;
        }

        if (!DoDepositCash(resolved, cash, user))
        {
            Deny(resolved, user, "shipyard-console-transaction-failed");
            return false;
        }

        return true;
    }

    public bool CanDepositCash(
        Entity<ShipyardConsoleComponent?> ent,
        EntityUid cash,
        EntityUid user,
        [NotNullWhen(false)] out string? reason)
    {
        reason = null;

        if (!Resolve(ent, ref ent.Comp, false))
        {
            reason = "shipyard-console-unavailable";
            return false;
        }

        if (!HasComp<CashComponent>(cash))
        {
            reason = "shipyard-console-invalid-cash";
            return false;
        }

        if (!_access.IsAllowed(user, ent))
        {
            reason = "shipyard-console-access-denied";
            return false;
        }

        if (IsConsoleBroken(ent))
        {
            reason = "shipyard-console-broken";
            return false;
        }

        if (!TryGetCashValue(cash, out var amount))
        {
            reason = "shipyard-console-invalid-cash";
            return false;
        }

        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
        {
            reason = "shipyard-console-station-not-found";
            return false;
        }

        if (!_cargo.TryGetAccount((station, bank), ent.Comp.Account, out var balance))
        {
            reason = "shipyard-console-account-not-found";
            return false;
        }

        if ((long) balance + amount > int.MaxValue)
        {
            reason = "shipyard-console-account-limit-reached";
            return false;
        }

        return true;
    }

    private bool DoDepositCash(Entity<ShipyardConsoleComponent> ent, EntityUid cash, EntityUid user)
    {
        if (!TryGetCashValue(cash, out var amount) ||
            _station.GetOwningStation(ent) is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank) ||
            !TryAdjustBankAccount((station, bank), ent.Comp.Account, amount))
        {
            return false;
        }

        _adminLogger.Add(
            LogType.Action,
            LogImpact.Medium,
            $"{ToPrettyString(user):player} deposited {amount} credits using {ToPrettyString(cash):cash} into "
            + $"{ent.Comp.Account} at {ToPrettyString(ent):console}.");
        QueueDel(cash);

        _audio.PlayPvs(ent.Comp.ConfirmSound, ent);
        _popup.PopupEntity(Loc.GetString("shipyard-console-credit-deposit", ("amount", amount)), ent, user);
        UpdateUi(ent);
        return true;
    }

    private bool TryGetCashValue(EntityUid cash, out int amount)
    {
        amount = 0;
        if (!HasComp<CashComponent>(cash))
            return false;

        var value = _pricing.GetPrice(cash);
        if (!double.IsFinite(value) || value <= 0 || value > int.MaxValue)
            return false;

        amount = (int) value;
        return amount > 0;
    }

    private bool TryAdjustBankAccount(
        Entity<StationBankAccountComponent> station,
        ProtoId<CargoAccountPrototype> account,
        int amount)
    {
        Entity<StationBankAccountComponent?> bank = (station.Owner, station.Comp);
        if (!_cargo.TryGetAccount(bank, account, out var balance))
            return false;

        var adjustedBalance = (long) balance + amount;
        if (adjustedBalance is < 0 or > int.MaxValue)
            return false;

        return _cargo.TryAdjustBankAccount(bank, account, amount);
    }

    private void Deny(
        Entity<ShipyardConsoleComponent> ent,
        EntityUid user,
        string message,
        params (string Key, object Value)[] args)
    {
        _audio.PlayPvs(ent.Comp.ErrorSound, ent);
        if (Exists(user))
            _popup.PopupEntity(Loc.GetString(message, args), ent, user);
        UpdateUi(ent);
    }

    private void Announce(
        Entity<ShipyardConsoleComponent> ent,
        string message,
        params (string Key, object Value)[] args)
    {
        _radio.SendRadioMessage(ent, Loc.GetString(message, args), ent.Comp.AnnouncementChannel, ent, escapeMarkup: false);
    }
}

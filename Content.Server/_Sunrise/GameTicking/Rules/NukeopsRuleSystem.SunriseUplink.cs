using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Store.Systems;
using Content.Server.Traitor.Uplink;
using Content.Shared.FixedPoint;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130
namespace Content.Server.GameTicking.Rules;

public sealed partial class NukeopsRuleSystem
{
    // Handles Sunrise-specific NukeOps uplink setup and commander TC distribution hooks.
    [Dependency] private UplinkSystem _uplinkSystem = default!;

    [ValidatePrototypeId<AntagPrototype>]
    private const string CommanderAntagProto = "NukeopsCommander";
    private const int FighterUplinkTc = 30;

    private bool TryDistributeExtraTcSunrise(Entity<NukeopsRuleComponent> nukieRule)
    {
        if (nukieRule.Comp.UplinkEnt is not { } commanderUplink ||
            !TryComp<StoreComponent>(commanderUplink, out var store))
            return false;

        if (nukieRule.Comp.RoundstartOperatives == 0)
            return true;

        var fightersCount = Math.Max(nukieRule.Comp.RoundstartOperatives - 1, 0);
        if (fightersCount == 0)
            return true;

        var bonusTc = GetCommanderWarBonusTc(fightersCount);
        _store.TryAddCurrency(new() { { TelecrystalCurrencyPrototype, bonusTc } }, commanderUplink, store);

        var msg = Loc.GetString("store-currency-war-boost-given", ("target", commanderUplink));
        _popupSystem.PopupEntity(msg, commanderUplink);
        return true;
    }

    private void OnAfterAntagEntSelectedSunrise(Entity<NukeopsRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        ent.Comp.RoundstartOperatives += 1;

        if (args.Def.PrefRoles.Contains(CommanderAntagProto))
        {
            var uplink = SetupUplink(args.EntityUid, 0, true);
            ent.Comp.UplinkEnt = uplink;

            if (uplink == null)
                return;

            var fightersAlreadySelected = Math.Max(ent.Comp.RoundstartOperatives - 1, 0);
            var totalTc = GetCommanderStartupTc(fightersAlreadySelected);
            var store = EnsureComp<StoreComponent>(uplink.Value);
            _store.TryAddCurrency(
                new Dictionary<string, FixedPoint2> { { TelecrystalCurrencyPrototype, totalTc } },
                uplink.Value,
                store);
            return;
        }

        _ = SetupUplink(args.EntityUid, FighterUplinkTc, true);

        if (ent.Comp.UplinkEnt == null)
            return;

        var giveTcCount = GetCommanderTcPerFighter();
        var commanderStore = EnsureComp<StoreComponent>(ent.Comp.UplinkEnt.Value);
        _store.TryAddCurrency(
            new Dictionary<string, FixedPoint2> { { TelecrystalCurrencyPrototype, giveTcCount } },
            ent.Comp.UplinkEnt.Value,
            commanderStore);
    }

    private EntityUid? SetupUplink(EntityUid user, FixedPoint2 balance, bool giveDiscounts)
    {
        var uplink = _uplinkSystem.FindUplinkByTag(user, NukeOpsUplinkTagPrototype);
        if (uplink == null)
            return null;

        _uplinkSystem.SetSunriseUplink(user, uplink.Value, balance, giveDiscounts);
        return uplink;
    }
}

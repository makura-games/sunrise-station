using Content.Shared.DoAfter;
using Content.Shared.Database;
using Content.Shared.Kitchen.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared._Sunrise.Random;
using Robust.Shared.Player;

namespace Content.Shared._Sunrise.Execution;

public abstract partial class SharedExecutionSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private RandomPredictedSystem _predictedRandom = default!;

    protected const float MeleeExecutionTimeModifier = 5.0f;
    protected const float SuicideFastChance = 0.25f;
    protected const float SuicideFastTimeMultiplier = 0.16f;
    protected const float GunExecutionTime = 6.0f;
    protected const float SuicideGunTimeMultiplier = 0.5f;
    protected const int GunExecutionShots = 1;
    protected const float AttackRateToSeconds = 1.0f;
    protected const string GunChamberContainerId = "gun_chamber";
    protected const string GunMagazineContainerId = "gun_magazine";
    protected const string StructuralDamageType = "Structural";

    protected static readonly string[] NonLethalAmmoIdTokens =
    {
        "Practice",
        "Rubber",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharpComponent, GetVerbsEvent<UtilityVerb>>(OnGetInteractionVerbsMelee);
        SubscribeLocalEvent<GunComponent, GetVerbsEvent<UtilityVerb>>(OnGetInteractionVerbsGun);
    }

    private void OnGetInteractionVerbsMelee(Entity<SharpComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!TryGetVerbContext(ref args, out var attacker, out var weapon, out var victim, out var suicide))
            return;

        if (!CanExecuteWithMelee(weapon, victim, attacker))
            return;

        UtilityVerb verb = new()
        {
            Act = () =>
            {
                TryStartMeleeExecutionDoafter(weapon, victim, attacker);
            },
            Impact = LogImpact.High,
            Text = suicide ? Loc.GetString("suicide-verb-name") : Loc.GetString("execution-verb-name"),
            Message = suicide ? Loc.GetString("suicide-verb-message") : Loc.GetString("execution-verb-message"),
        };

        args.Verbs.Add(verb);
    }

    private void OnGetInteractionVerbsGun(Entity<GunComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!TryGetVerbContext(ref args, out var attacker, out var weapon, out var victim, out var suicide))
            return;

        if (!CanExecuteWithGun(weapon, victim, attacker))
            return;

        UtilityVerb verb = new()
        {
            Act = () =>
            {
                TryStartGunExecutionDoafter(weapon, victim, attacker);
            },
            Impact = LogImpact.High,
            Text = suicide ? Loc.GetString("suicide-verb-name") : Loc.GetString("execution-verb-name"),
            Message = suicide ? Loc.GetString("suicide-verb-message") : Loc.GetString("execution-verb-message"),
        };

        args.Verbs.Add(verb);
    }

    protected void TryStartMeleeExecutionDoafter(EntityUid weapon, EntityUid victim, EntityUid attacker)
    {
        if (!CanExecuteWithMelee(weapon, victim, attacker))
            return;

        if (!TryComp<MeleeWeaponComponent>(weapon, out var melee))
            return;

        var executionTime = AttackRateToSeconds / melee.AttackRate * MeleeExecutionTimeModifier;
        var suicide = attacker == victim;

        if (suicide && ShouldUseFastSuicide(weapon))
            executionTime *= SuicideFastTimeMultiplier;

        var recipientKey = suicide
            ? "suicide-popup-melee-initial-internal"
            : "execution-popup-melee-initial-internal";

        var othersKey = suicide
            ? "suicide-popup-melee-initial-external"
            : "execution-popup-melee-initial-external";

        ShowExecutionPopupPredicted(recipientKey, othersKey, PopupType.Medium, PopupType.MediumCaution, attacker, victim, weapon);

        var doAfter =
            new DoAfterArgs(EntityManager, attacker, executionTime, new ExecutionDoAfterEvent(), weapon, target: victim, used: weapon)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            };

        _doAfterSystem.TryStartDoAfter(doAfter);
    }

    protected void TryStartGunExecutionDoafter(EntityUid weapon, EntityUid victim, EntityUid attacker)
    {
        if (!CanExecuteWithGun(weapon, victim, attacker))
            return;

        if (!TryComp<GunComponent>(weapon, out var gunComponent))
            return;

        var shotAttempted = new ShotAttemptedEvent
        {
            User = attacker,
            Used = (weapon, gunComponent),
        };

        RaiseLocalEvent(weapon, ref shotAttempted);
        if (shotAttempted.Cancelled)
        {
            if (shotAttempted.Message != null)
            {
                _popupSystem.PopupPredicted(
                    shotAttempted.Message,
                    weapon,
                    attacker,
                    Filter.Entities(attacker),
                    false);
            }

            return;
        }

        var recipientKey = attacker == victim
            ? "suicide-popup-gun-initial-internal"
            : "execution-popup-gun-initial-internal";

        var othersKey = attacker == victim
            ? "suicide-popup-gun-initial-external"
            : "execution-popup-gun-initial-external";

        ShowExecutionPopupPredicted(recipientKey, othersKey, PopupType.Medium, PopupType.MediumCaution, attacker, victim, weapon);

        var executionTime = attacker == victim ? GunExecutionTime * SuicideGunTimeMultiplier : GunExecutionTime;

        if (attacker == victim && ShouldUseFastSuicide(weapon))
            executionTime *= SuicideFastTimeMultiplier;

        var doAfter =
            new DoAfterArgs(EntityManager,
                attacker,
                executionTime,
                new ExecutionDoAfterEvent(),
                weapon,
                target: victim,
                used: weapon)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true
            };

        _doAfterSystem.TryStartDoAfter(doAfter);
    }

    private bool ShouldUseFastSuicide(EntityUid weapon)
    {
        return _predictedRandom.ProbForEntity(weapon, SuicideFastChance);
    }

    private void ShowExecutionPopupPredicted(
        string recipientLocString,
        string othersLocString,
        PopupType recipientType,
        PopupType othersType,
        EntityUid attacker,
        EntityUid victim,
        EntityUid weapon)
    {
        var recipientMessage = Loc.GetString(recipientLocString, ("attacker", attacker), ("victim", victim), ("weapon", weapon));
        var othersMessage = Loc.GetString(othersLocString, ("attacker", attacker), ("victim", victim), ("weapon", weapon));

        _popupSystem.PopupPredicted(recipientMessage, attacker, attacker, Filter.Entities(attacker), false, recipientType);
        _popupSystem.PopupPredicted(othersMessage, attacker, null, Filter.PvsExcept(attacker), true, othersType);
    }
}

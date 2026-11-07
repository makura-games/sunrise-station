using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using Content.Server.Antag.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems;

/// <summary>
/// Sunrise — admin smite verb that assigns all Traitor antagonists a kill objective
/// targeting the selected player.
/// </summary>
public sealed partial class AdminVerbSystem
{
    [Dependency] private SharedJobSystem _jobSystem = default!;
    [Dependency] private TargetObjectiveSystem _targetObjective = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private ObjectivesSystem _objectivesSystem = default!;
    [Dependency] private EntityQuery<AntagSelectionComponent> _antagSelectionQuery = default!;
    private const string AdminBountyKillObjectiveProto = "AdminBountyKillObjective";

    private static readonly SpriteSpecifier BountyVerbIcon =
        new SpriteSpecifier.Rsi(new("/Textures/Interface/Misc/job_icons.rsi"), "Syndicate");

    private void AddBountySmiteVerb(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.HasAdminFlag(player, AdminFlags.Fun))
            return;

        var bountyName = Loc.GetString("admin-smite-traitor-bounty-name");
        var target = args.Target;
        Verb bounty = new()
        {
            Text = bountyName,
            Category = VerbCategory.Smite,
            Icon = BountyVerbIcon,
            Act = () =>
            {
                AssignTraitorBounty(target, player);
            },
            Impact = LogImpact.Extreme,
            Message = string.Join(": ", bountyName, Loc.GetString("admin-smite-traitor-bounty-description")),
        };
        args.Verbs.Add(bounty);
    }

    private void AssignTraitorBounty(EntityUid target, ICommonSession admin)
    {
        if (!_mindSystem.TryGetMind(target, out var targetMindId, out var targetMind))
        {
            _mindSystem.MakeSentient(target);
            var newMind = _mindSystem.CreateMind(null, Name(target));
            _mindSystem.TransferTo(newMind, target);
            targetMindId = newMind;
            targetMind = Comp<MindComponent>(newMind);
        }

        var targetName = targetMind.CharacterName ?? Name(target);
        var jobName = _jobSystem.MindTryGetJobName(targetMindId);

        var traitorCount = 0;
        var query = EntityQueryEnumerator<TraitorRuleComponent>();
        while (query.MoveNext(out var ruleUid, out _))
        {
            if (!_antagSelectionQuery.HasComponent(ruleUid))
                continue;

            foreach (var mind in _antag.GetAntagMinds(ruleUid))
            {
                // Don't assign a kill objective targeting themselves
                if (mind.Owner == targetMindId)
                    continue;

                // Skip if this traitor already has a kill objective on the same target
                if (HasKillObjectiveOn(mind.Comp, targetMindId))
                    continue;

                // Create the objective through the objective system (validates ObjectiveComponent, runs assignment hooks)
                if (_objectivesSystem.TryCreateObjective(mind.Owner, mind.Comp, AdminBountyKillObjectiveProto) is not { } objectiveUid)
                    continue;

                // Set the target on the objective (must be done after creation since it's a runtime value)
                _targetObjective.SetTarget(objectiveUid, targetMindId);

                // Set the entity name (title) for the objective
                var title = Loc.GetString("objective-condition-admin-bounty-kill-title",
                    ("targetName", targetName),
                    ("job", jobName));
                _metaSystem.SetEntityName(objectiveUid, title);

                // Add the objective to the traitor's mind
                _mindSystem.AddObjective(mind.Owner, mind.Comp, objectiveUid);

                // Notify the traitor if they have a session
                if (mind.Comp.UserId is { } userId &&
                    _playerManager.TryGetSessionById(userId, out var session))
                {
                    _chatManager.DispatchServerMessage(session,
                        Loc.GetString("admin-bounty-card-new-objective",
                            ("targetName", targetName),
                            ("job", jobName)));
                }

                traitorCount++;
            }
        }

        _chatManager.DispatchServerMessage(admin,
            Loc.GetString("admin-bounty-card-result",
                ("count", traitorCount),
                ("targetName", targetName)));
    }

    /// <summary>
    /// Checks if the mind already has a KillPersonCondition objective targeting the given mind.
    /// </summary>
    private bool HasKillObjectiveOn(MindComponent mind, EntityUid targetMindId)
    {
        foreach (var objective in mind.Objectives)
        {
            if (!TryComp<KillPersonConditionComponent>(objective, out _))
                continue;

            if (!TryComp<TargetObjectiveComponent>(objective, out var targetObj))
                continue;

            if (targetObj.Target == targetMindId)
                return true;
        }

        return false;
    }
    public void RandomDeath(EntityUid target)
    {
        var badEvents = new List<Action<EntityUid>>
        {
            VomitToDeath,
            BluespaceAway,
            BleedOut,
            Irradiate,
            Scorched,
            Shock,
        };

        var random = new Random();
        var randomEvent = badEvents[random.Next(badEvents.Count)];
        randomEvent(target);
    }

    private FixedPoint2 GetDamageToKill(EntityUid target)
    {
        var random = new Random();
        var multiplier = (float)(random.NextDouble() * 6.0 + 4.0); // Random float between 4.0 and 10.0
        var damageToKill = _mobThresholdSystem.TryGetThresholdForState(target, MobState.Dead, out var deadThreshold)
            ? deadThreshold.Value * multiplier
            : FixedPoint2.New(100);
        return FixedPoint2.New((int)Math.Round(damageToKill.Float(), MidpointRounding.AwayFromZero));
    }

    private void Irradiate(EntityUid target)
    {
        _popupSystem.PopupEntity("3.6 roentgen, not great, not terrible...", target, target, PopupType.LargeCaution);
        var damageSpecifier = new DamageSpecifier()
        {
            DamageDict = new()
            {
                { "Radiation", GetDamageToKill(target) }
            }
        };
        _damageable.SetDamage(target, damageSpecifier);
    }

    private void Scorched(EntityUid target)
    {
        if (TryComp<FlammableComponent>(target, out var flammable))
        {
            // Popup message to notify the target
            _popupSystem.PopupEntity("You smell gas, Smoking kills...", target, target, PopupType.LargeCaution);

            // Ignite the target
            flammable.FireStacks = 3;
            _flammableSystem.Ignite(target, target);

            // Wait for a couple of seconds before applying the damage
            Timer.Spawn(TimeSpan.FromSeconds(2), () =>
            {
                // Apply heat damage after the delay
                var damageSpecifier = new DamageSpecifier()
                {
                    DamageDict = new()
                    {
                        { "Heat", GetDamageToKill(target) - 50 }
                    }
                };
                _damageable.SetDamage(target, damageSpecifier);
            });
        }
    }

    private void Shock(EntityUid target)
    {
        _popupSystem.PopupEntity("You trip on a loose wire...", target, target, PopupType.LargeCaution);
        var damageToKill = GetDamageToKill(target);
        _electrocutionSystem.TryDoElectrocution(target,
            null,
            (int)damageToKill,
            TimeSpan.FromSeconds(30),
            refresh: true,
            ignoreInsulation: true);
    }

    private void VomitToDeath(EntityUid target)
    {
        _popupSystem.PopupEntity("You start vomiting uncontrollably...", target, target, PopupType.LargeCaution);
        _vomitSystem.Vomit(target, -1000, -1000);
        var damageSpecifier = new DamageSpecifier()
        {
            DamageDict = new()
            {
                { "Poison", GetDamageToKill(target) }
            }
        };
        _damageable.SetDamage(target, damageSpecifier);
    }

    private void BluespaceAway(EntityUid target)
    {
        // Notify the player about the teleportation
        _popupSystem.PopupEntity("You feel a strange sensation as you are teleported away...", target, target, PopupType.LargeCaution);

        // Get the current position of the target
        var currentCoordinates = _transformSystem.GetMapCoordinates(target);

        // Spawn the bluespace effect at the current location
        var bluespaceEffect = "EffectFlashBluespace"; // Replace with the actual prototype ID
        Spawn(bluespaceEffect, currentCoordinates);

        // Trigger the explosion after teleportation
        QueueDel(target);
    }

    private void BleedOut(EntityUid target)
    {
        _popupSystem.PopupEntity("You start bleeding profusely...", target, target, PopupType.LargeCaution);

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            _bloodstreamSystem.SpillAllSolutions((target, bloodstream));
        }

        var damageSpecifier = new DamageSpecifier()
        {
            DamageDict = new()
            {
                { "Slash", GetDamageToKill(target) }
            }
        };

        _damageable.SetDamage(target, damageSpecifier);
    }
}

using Content.Shared._Sunrise.Antags.Abductor;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Server.Speech.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Coordinates;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Antag;
using Content.Shared._Sunrise.VentCraw;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Server.Objectives.Components;
using Content.Shared.Damage.Systems;

namespace Content.Server._Sunrise.Antags.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private IGameTiming _time = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private SunriseHumanoidMarkingSystem _sunriseMarking = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    private static readonly EntProtoId DefaultRule = "AbductorVictim";


    private float _delayAccumulator = 0f;
    private readonly Stopwatch _stopwatch = new();
    private readonly DamageSpecifier _passiveHealing = new();

    public void InitializeOrgans()
    {
        SubscribeLocalEvent<AbductorVictimRoleComponent, GetBriefingEvent>(OnGetBriefing);
        SubscribeLocalEvent<AbductorOrganComponent, SurgeryOrganImplantationCompleted>(OnAbductorOrganImplanted);
        SubscribeLocalEvent<AbductorOrganComponent, SurgeryOrganExtracted>(OnAbductorOrganExtracted);

        foreach (var specif in _prototypeManager.EnumeratePrototypes<DamageTypePrototype>())
            _passiveHealing.DamageDict.Add(specif.ID, -1);
        _stopwatch.Start();
    }

    private void OnGetBriefing(Entity<AbductorVictimRoleComponent> ent, ref GetBriefingEvent ev)
    {
        ev.Append(Loc.GetString("abductor-victim-role-greeting"));
    }
    private void OnAbductorOrganImplanted(Entity<AbductorOrganComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {

        EnsureComp<AbductorVictimComponent>(args.Body, out var victim);
        victim.Organ = ent.Comp.Organ;

        if (ent.Comp.Organ == AbductorOrganType.Vent)
            AddComp<VentCrawlerComponent>(args.Body);

        if (ent.Comp.Organ == AbductorOrganType.Pacified)
            AddComp<PacifiedComponent>(args.Body);

        if (ent.Comp.Organ == AbductorOrganType.Liar)
        {
            EnsureComp<ReplacementAccentComponent>(args.Body, out var accent);
            accent.Accent = "liar";
        }

        if (ent.Comp.Organ == AbductorOrganType.TraitorGoal)
        {
            if (!TryComp<ActorComponent>(args.Body, out var actor))
                return;

            _antag.ForceMakeAntag<AbductorVictimRuleComponent>(actor.PlayerSession, DefaultRule);
        }

        if (ent.Comp.Organ == AbductorOrganType.Owo)
            victim.TransformationTime += _time.CurTime;
    }
    private void OnAbductorOrganExtracted(Entity<AbductorOrganComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (TryComp<AbductorVictimComponent>(args.Body, out var victim))
            if (victim.Organ == ent.Comp.Organ)
                victim.Organ = AbductorOrganType.None;

        if (ent.Comp.Organ == AbductorOrganType.Vent)
            RemComp<VentCrawlerComponent>(args.Body);

        if (ent.Comp.Organ == AbductorOrganType.Pacified)
            RemComp<PacifiedComponent>(args.Body);

        if (ent.Comp.Organ == AbductorOrganType.Liar)
            RemComp<ReplacementAccentComponent>(args.Body);

        if (ent.Comp.Organ == AbductorOrganType.TraitorGoal)
        {
            if (!_mind.TryGetMind(args.Body, out var mindId, out var mind))
                return;

            _role.MindRemoveRole(mindId, "AbductorVictimRole");

            var toRemove = new List<EntityUid>();
            foreach (var obj in mind.Objectives)
            {
                if (!TryComp<RoleRequirementComponent>(obj, out var req))
                    continue;

                if (req.Roles.Contains("AbductorVictimRole"))
                    toRemove.Add(obj);
            }

            foreach (var obj in toRemove)
            {
                mind.Objectives.Remove(obj);
            }
        }

        if (ent.Comp.Organ == AbductorOrganType.Owo)
        {
            RemComp<AbductorOwoTransformatedComponent>(args.Body);
            RemComp<OwOAccentComponent>(args.Body);
            _sunriseMarking.RemoveMarking(args.Body, "CatEars");
            _sunriseMarking.RemoveMarking(args.Body, "CatTail");
        }
    }
    public override void Update(float frameTime)
    {
        _delayAccumulator += frameTime;

        if (_delayAccumulator > 3)
        {
            _delayAccumulator = 0;
            _stopwatch.Restart();

            var query = EntityQueryEnumerator<AbductorVictimComponent>();
            while (query.MoveNext(out var uid, out var victim) && _stopwatch.Elapsed < TimeSpan.FromMilliseconds(0.5))
            {
                if (victim.Organ != AbductorOrganType.None)
                    Do(uid, victim);
            }
        }
    }

    private void Do(EntityUid uid, AbductorVictimComponent victim)
    {
        if (HasComp<AbductorOwoTransformatedComponent>(uid))
            return;

        var curTime = _time.CurTime;

        switch (victim.Organ)
        {
            case AbductorOrganType.Health:

                if (curTime - victim.LastActivation < TimeSpan.FromSeconds(3))
                    return;

                victim.LastActivation = curTime;
                _damageable.TryChangeDamage(uid, _passiveHealing);
                break;

            case AbductorOrganType.Gravity:

                if (curTime - victim.LastActivation < TimeSpan.FromSeconds(120))
                    return;

                victim.LastActivation = curTime;
                var gravity = SpawnAttachedTo("AbductorGravityGlandGravityWell", Transform(uid).Coordinates);
                _xformSys.SetParent(gravity, uid);
                break;

            case AbductorOrganType.Egg:

                if (curTime - victim.LastActivation < TimeSpan.FromSeconds(120))
                    return;

                victim.LastActivation = curTime;
                SpawnAttachedTo("FoodEggChickenFertilized", Transform(uid).Coordinates);
                break;

            case AbductorOrganType.Spider:

                if (curTime - victim.LastActivation < TimeSpan.FromSeconds(240))
                    return;

                victim.LastActivation = curTime;
                SpawnAttachedTo("EggSpiderFertilized", Transform(uid).Coordinates);
                break;

            case AbductorOrganType.Ephedrine:

                if (curTime - victim.LastActivation < TimeSpan.FromSeconds(120))
                    return;

                victim.LastActivation = curTime;
                if (_solutions.TryGetInjectableSolution(uid, out var injectable, out _))
                    _solutions.TryAddReagent(injectable.Value, "Ephedrine", 5f);
                break;

            case AbductorOrganType.Owo:
                if (curTime > victim.TransformationTime)
                {
                    if (HasComp<OwOAccentComponent>(uid))
                        return;
                    EnsureComp<AbductorOwoTransformatedComponent>(uid);
                    _popup.PopupEntity(Loc.GetString("owo-organ-transformation"), uid, PopupType.LargeCaution);
                    _audioSystem.PlayPvs(victim.Mew, uid);
                    SpawnAttachedTo("RMCExplosionEffectGrenadeShockWave", uid.ToCoordinates());
                    EnsureComp<OwOAccentComponent>(uid);
                    _sunriseMarking.AddMarking(uid, "CatTail");
                    _sunriseMarking.AddMarking(uid, "CatEars"); // На ласте еще добавлять костюм горничной и лапки, why not
                }

                break;

            case AbductorOrganType.EMP:

                if (curTime - victim.LastActivation < TimeSpan.FromSeconds(120))
                    return;

                victim.LastActivation = curTime;
                SpawnAttachedTo("AdminInstantEffectEMP", Transform(uid).Coordinates);
                break;


            default:
                break;
        }
    }
}

using Content.Server._Sunrise.FleshCult.GameRule;
using Content.Shared._Sunrise.FleshCult;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.FleshCult;

public sealed partial class FleshCultSystem
{
    private static readonly ProtoId<ReagentPrototype> CarolReagent = "Carol";
    private static readonly ProtoId<TagPrototype> FleshTag = "Flesh";
    private static readonly FixedPoint2 CarolInfectionThreshold = FixedPoint2.New(5);

    private void InitializeVirus()
    {
        SubscribeLocalEvent<PendingFleshCultistComponent, MapInitEvent>(OnPendingMapInit);
        SubscribeLocalEvent<BloodstreamComponent, SolutionChangedEvent>(OnBloodstreamSolutionChanged);
    }

    private void OnBloodstreamSolutionChanged(Entity<BloodstreamComponent> ent, ref SolutionChangedEvent args)
    {
        TryInfectWithCarol(ent, args.Solution);
    }

    private void OnPendingMapInit(EntityUid uid, PendingFleshCultistComponent component, MapInitEvent args)
    {
        component.NextParalyze = _timing.CurTime + TimeSpan.FromSeconds(1f);
        component.NextScream = _timing.CurTime + TimeSpan.FromSeconds(1f);
    }

    public bool TryInfectWithCarol(Entity<BloodstreamComponent> ent, Entity<SolutionComponent> solution)
    {
        if (!CanInfectWithCarol(ent, solution))
            return false;

        EnsureComp<PendingFleshCultistComponent>(ent);
        return true;
    }

    public bool CanInfectWithCarol(Entity<BloodstreamComponent> ent, Entity<SolutionComponent> solution)
    {
        if (!TryComp<MobStateComponent>(ent, out var mobState) || mobState.CurrentState == MobState.Dead)
            return false;

        if (HasComp<PendingFleshCultistComponent>(ent) ||
            HasComp<MindShieldComponent>(ent) ||
            _tagSystem.HasTag(ent, FleshTag))
        {
            return false;
        }

        if (!_solutionContainerSystem.TryGetSolution(ent.Owner, ent.Comp.BloodSolutionName, out var bloodSolution) ||
            bloodSolution.Value.Owner != solution.Owner)
        {
            return false;
        }

        return solution.Comp.Solution.GetTotalPrototypeQuantity(CarolReagent) >= CarolInfectionThreshold;
    }

    public void UpdateVirus(float frameTime)
    {
        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<PendingFleshCultistComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.CurrentStage == PendingFleshCultistStage.Final)
                continue;

            comp.Accumulator += frameTime;

            var stageTimer = comp.CurrentStage switch
            {
                PendingFleshCultistStage.First => comp.FirstStageTimer,
                PendingFleshCultistStage.Second => comp.SecondStageTimer,
                _ => 0f
            };

            if (comp.Accumulator >= stageTimer)
            {
                comp.Accumulator = 0f;
                comp.CurrentStage = GetNextStage(comp.CurrentStage);
            }

            switch (comp.CurrentStage)
            {
                case PendingFleshCultistStage.First:
                    if (comp.NextScream <= curTime)
                    {
                        comp.NextScream = curTime + TimeSpan.FromSeconds(comp.ScreamInterval);
                        _chatSystem.TryEmoteWithChat(uid, "Scream");
                    }
                    if (comp.NextStutter <= curTime)
                    {
                        comp.NextStutter = curTime + TimeSpan.FromSeconds(comp.ScreamInterval);
                        _stuttering.DoStutter(uid, TimeSpan.FromSeconds(comp.StutterTime), true);
                    }
                    break;
                case PendingFleshCultistStage.Second:
                    if (comp.NextParalyze <= curTime)
                    {
                        comp.NextParalyze = curTime + TimeSpan.FromSeconds(comp.ParalyzeInterval);
                        _stunSystem.TryAddParalyzeDuration(uid, TimeSpan.FromSeconds(comp.ParalyzeTime));
                    }
                    if (comp.NextJitter <= curTime)
                    {
                        comp.NextJitter = curTime + TimeSpan.FromSeconds(comp.JitterInterval);
                        _jittering.DoJitter(uid, TimeSpan.FromSeconds(comp.JitterTime), true);
                    }
                    break;
                case PendingFleshCultistStage.Third:
                {
                    if (!HasComp<MindContainerComponent>(uid) || !TryComp<ActorComponent>(uid, out var targetActor))
                        return;

                    var targetPlayer = targetActor.PlayerSession;

                    if (HasComp<MindShieldComponent>(uid))
                    {
                        // SUNRISE-TODO: Сделать это внутри системы майншилда
                        _popup.PopupEntity("Активация самоуничтожения импланта защиты разума", uid, PopupType.LargeCaution);
                        _gibbing.Gib(uid, true);
                        _explosionSystem.QueueExplosion(uid, "Default", 50, 5, 30, canCreateVacuum: false);
                        break;
                    }

                    if (!TryComp<HumanoidProfileComponent>(uid, out var humanoidAppearance))
                        break;

                    if (!_speciesWhitelist.Contains(humanoidAppearance.Species))
                    {
                        RemCompDeferred<PendingFleshCultistComponent>(uid);
                        break;
                    }

                    _antag.ForceMakeAntag<FleshCultRuleComponent>(targetPlayer, DefaultFleshCultRule);

                    comp.CurrentStage = PendingFleshCultistStage.Final;
                    RemCompDeferred<PendingFleshCultistComponent>(uid);
                    break;
                }
            }
        }
    }

    private PendingFleshCultistStage GetNextStage(PendingFleshCultistStage currentStage)
    {
        return currentStage switch
        {
            PendingFleshCultistStage.First => PendingFleshCultistStage.Second,
            PendingFleshCultistStage.Second => PendingFleshCultistStage.Third,
            PendingFleshCultistStage.Third => PendingFleshCultistStage.Final,
            _ => currentStage,
        };
    }
}

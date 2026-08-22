using Content.Shared.Humanoid;
using Content.Shared.DoAfter;
using Content.Shared.Nutrition;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Popups;
using Content.Shared.Actions;
using Content.Shared.EntityEffects.Effects.Solution;

namespace Content.Shared._Sunrise.Medical.PsychologistSystem;

public sealed partial class PsychologistSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;

    private readonly string _actionProto = "PsychologistAlcoholBlock";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsychologistBlockAlcoholComponent, ComponentStartup>(OnPsychologistBlockAlcohol);

        SubscribeLocalEvent<HumanoidProfileComponent, AlcoholBlockEvent>(OnAlcoholBlockTry);
        SubscribeLocalEvent<HumanoidProfileComponent, DoAfterAlcoholBlockEvent>(DoAfterAlcoholBlock);

        SubscribeLocalEvent<SolutionIngestBlockerComponent, BeforeIngestedEvent>(OnDrink);
    }

    private void DoAfterAlcoholBlock(Entity<HumanoidProfileComponent> ent, ref DoAfterAlcoholBlockEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Target == null)
            return;

        if (HasComp<SolutionIngestBlockerComponent>(args.Target))
        {
            _popupSystem.PopupEntity(Loc.GetString("psychologist-alcoholblock-removed", ("target", args.Target)), ent);
            RemComp<SolutionIngestBlockerComponent>(args.Target.Value);
            return;
        }

        _popupSystem.PopupEntity(Loc.GetString("psychologist-alcoholblock-applied", ("target", args.Target)), ent);
        AddComp<SolutionIngestBlockerComponent>(args.Target.Value);
    }

    private void OnAlcoholBlockTry(Entity<HumanoidProfileComponent> ent, ref AlcoholBlockEvent args)
    {
        if (!TryComp<HumanoidProfileComponent>(args.Target, out var profile))
            return;

        if (profile.Species.Id == "Dwarf")
        {
            _popupSystem.PopupEntity(Loc.GetString("psychologist-alcoholblock-dwarf-forbidden"), ent);
            return;
        }

        if (_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, args.Action.Comp.UseDelay ?? TimeSpan.FromSeconds(30),
            new DoAfterAlcoholBlockEvent(), eventTarget: args.Target, target: args.Target, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        }))
        {
            args.Handled = true;
        }
        else
        {
            args.Handled = false;
        }

    }
    private void OnDrink(Entity<SolutionIngestBlockerComponent> ent, ref BeforeIngestedEvent args)
    {
        if (args.Solution != null)
        {
            foreach (var cont in args.Solution.Contents)
            {
                if (cont.Reagent.ToString() == ent.Comp.ReagentForBlock)
                {
                    args.Cancelled = true;
                    return;
                }
                var reagent = _prototypeManager.Index<ReagentPrototype>($"{cont.Reagent}");

                if (reagent.Metabolisms != null)
                {
                    foreach (var metabolism in reagent.Metabolisms.Metabolisms.Values)
                    {
                        foreach (var effect in metabolism.Effects)
                        {
                            if (effect is AdjustReagent adjust && adjust.Reagent == ent.Comp.ReagentForBlock)
                            {
                                args.Cancelled = true;
                                return;
                            }
                        }
                    }
                }
                else
                {
                    return;
                }
            }
        }
        else
        {
            return;
        }
    }
    private void OnPsychologistBlockAlcohol(Entity<PsychologistBlockAlcoholComponent> ent, ref ComponentStartup args)
    {
        _actionsSystem.AddAction(ent.Owner, _actionProto);
    }

}

using Content.Shared._Sunrise.Smell;
using Content.Shared._Sunrise.Smell.Components;
using Content.Shared._Sunrise.Smell.Prototypes;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Implants;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Smoking;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Smell;

/// <summary>
/// Scent source event handler system: reacts to events (ERP, damage,
/// ScentEmitter, finishing off a critical target) and records the acquired scent
/// into the bearer's ScentComponent via AddTemporaryScent.
/// </summary>
public sealed partial class ScentAcquisitionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SmellPrototypeCacheSystem _cache = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    /// <summary>
    /// Marker reagent of tobacco products; its presence in the smoking solution
    /// distinguishes tobacco from other fillings (drugs etc.).
    /// </summary>
    private static readonly ReagentId _nicotineReagent = new("Nicotine", null);

    public override void Initialize()
    {
        SubscribeLocalEvent<ScentEmitterComponent, GotEquippedEvent>(OnScentEmitterEquipped);
        SubscribeLocalEvent<ScentEmitterComponent, GotEquippedHandEvent>(OnScentEmitterPickedUp);
        SubscribeLocalEvent<ScentEmitterComponent, ImplantImplantedEvent>(OnScentEmitterImplanted);
        SubscribeLocalEvent<ScentEmitterComponent, IgnitedEvent>(OnScentEmitterIgnited);
        SubscribeLocalEvent<ScentComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<ScentOnAttackedComponent, AttackedEvent>(OnAttacked);
    }

    /// <summary>
    /// Scent-emitting item equipped into a clothing slot. Reacts to SpecificSlot
    /// (checks the required slot) and AnySlot modes. Hands mode is skipped here.
    /// </summary>
    private void OnScentEmitterEquipped(Entity<ScentEmitterComponent> ent, ref GotEquippedEvent args)
    {
        // курительные предметы пахнут только когда горят и это табак;
        // незажжённая сигарета/трубка запаха дыма не имеет
        if (!ShouldEmitOnCarry(ent))
            return;

        switch (ent.Comp.Spot)
        {
            case ScentEmitSpot.Hands:
                return; // руки обрабатывает отдельное событие.
            case ScentEmitSpot.SpecificSlot:
                if (args.Slot != ent.Comp.Slot)
                    return;
                break;
            case ScentEmitSpot.AnySlot:
                break; // любой слот -> даём запах.
        }

        AddTemporaryScent(args.EquipTarget, ent.Comp.Scent, ent.Comp.Duration);
    }

    /// <summary>
    /// Picking up a scent-emitting item into a hand: fires for Hands and AnySlot modes.
    /// </summary>
    private void OnScentEmitterPickedUp(Entity<ScentEmitterComponent> ent, ref GotEquippedHandEvent args)
    {
        // курительные предметы пахнут только когда горят и это табак.
        if (!ShouldEmitOnCarry(ent))
            return;

        if (ent.Comp.Spot != ScentEmitSpot.Hands && ent.Comp.Spot != ScentEmitSpot.AnySlot)
            return;

        AddTemporaryScent(args.User, ent.Comp.Scent, ent.Comp.Duration);
    }

    /// <summary>
    /// Injecting an implant that carries a scent grants the recipient
    /// a temporary scent dose. The implanted device itself no longer emits:
    /// skin and the implant shell hide it.
    /// </summary>
    private void OnScentEmitterImplanted(Entity<ScentEmitterComponent> ent, ref ImplantImplantedEvent args)
    {
        AddTemporaryScent(args.Implanted, ent.Comp.Scent, ent.Comp.Duration);
    }

    /// <summary>
    /// Igniting a tobacco product (cigarette, cigar, pipe) gives its current
    /// bearer a smoke scent dose. Non-nicotine fillings (drugs etc.) are skipped:
    /// their smell comes from status effects when inhaled.
    /// </summary>
    private void OnScentEmitterIgnited(Entity<ScentEmitterComponent> ent, ref IgnitedEvent args)
    {
        if (!TryComp<SmokableComponent>(ent, out var smokable))
            return;

        // Фильтр «это табак»: в курительном растворе должен быть никотин.
        if (!ContainsNicotine(ent, smokable))
            return;

        if (FindSmoker(ent.Owner) is { } bearer)
            AddTemporaryScent(bearer, ScentIds.Smoke, ent.Comp.Duration);
    }

    /// <summary>
    /// Checks incoming damage and adds three scents: blood (slashes and piercings),
    /// poison and bruises.
    /// </summary>
    private void OnDamageChanged(Entity<ScentComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        var dict = _damageable.GetAllDamage((ent.Owner, args.Damageable)).DamageDict;

        FixedPoint2 cuts = FixedPoint2.Zero;
        if (dict.TryGetValue("Slash", out var slash))
            cuts += slash;
        if (dict.TryGetValue("Piercing", out var piercing))
            cuts += piercing;

        if (cuts > _cache.Config.WoundScentThreshold)
            AddTemporaryScent((ent.Owner, ent.Comp), ScentIds.Blood, _cache.Config.WoundScentDuration);

        if (dict.TryGetValue("Blunt", out var blunt) && blunt > _cache.Config.WoundScentThreshold)
            AddTemporaryScent((ent.Owner, ent.Comp), ScentIds.Bruise, _cache.Config.WoundScentDuration);

        if (dict.TryGetValue("Poison", out var poison) && poison > _cache.Config.PoisonScentThreshold)
            AddTemporaryScent((ent.Owner, ent.Comp), ScentIds.Poison, _cache.Config.PoisonScentDuration);
    }

    /// <summary>
    /// Adds the killer's scent when damaging a critical-state target.
    /// Works with any melee weapon (crowbar, knife, etc.); the owner is the event User.
    /// Repeated hits simply refresh the single scent's timer (AddTemporaryScent
    /// replaces instead of duplicating).
    /// </summary>
    private void OnAttacked(Entity<ScentOnAttackedComponent> ent, ref AttackedEvent args)
    {
        if (!_mobState.IsCritical(ent.Owner))
        {
            return;
        }

        if (!HasComp<ScentComponent>(args.User))
            return;

        AddTemporaryScent(args.User, ScentIds.OtherBlood, _cache.Config.OtherBloodScentDuration);
    }

    /// <summary>
    /// Public API for scent sources (chemistry, smoking, ERP): add a temporary scent.
    /// Re-applying the same scent replaces the entry instead of duplicating it.
    /// </summary>
    public void AddTemporaryScent(Entity<ScentComponent?> ent, ProtoId<ScentPrototype> scent, TimeSpan duration)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var entry = new ActiveTemporaryScent
        {
            Scent = scent,
            StartTime = _timing.CurTime,
            Duration = duration,
        };

        for (int i = 0; i < ent.Comp.TemporaryScents.Count; i++)
        {
            if (ent.Comp.TemporaryScents[i].Scent == scent)
            {
                ent.Comp.TemporaryScents[i] = entry;
                return;
            }
        }

        ent.Comp.TemporaryScents.Add(entry);
    }

    /// <summary>
    /// Whether the burning filling of this smokable is tobacco
    /// (the smoking solution contains nicotine). Drug fillings are excluded:
    /// their smell comes from status effects when inhaled.
    /// </summary>
    private bool ContainsNicotine(Entity<ScentEmitterComponent> ent, SmokableComponent smokable)
    {
        return _solution.TryGetSolution(ent.Owner, smokable.Solution, out _, out var solution)
            && solution.ContainsReagent(_nicotineReagent);
    }

    /// <summary>
    /// Whether carrying this emitter right now should produce a scent:
    /// non-smoking items (grenades etc.) always do; tobacco products only
    /// while lit and containing nicotine.
    /// </summary>
    private bool ShouldEmitOnCarry(Entity<ScentEmitterComponent> ent)
    {
        if (!TryComp<SmokableComponent>(ent, out var smokable))
            return true;

        if (smokable.State != SmokableState.Lit)
            return false;

        return ContainsNicotine(ent, smokable);
    }

    /// <summary>
    /// Finds the mob currently holding this item: worn and held items are parented
    /// directly to the mob (mask slot, hands). Items lying on tables or stored
    /// in containers are ignored.
    /// </summary>
    private EntityUid? FindSmoker(EntityUid item)
    {
        var parent = Transform(item).ParentUid;

        return parent.IsValid() && HasComp<MobStateComponent>(parent)
            ? parent : null;
    }
}

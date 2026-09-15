using Content.Shared._Sunrise.Implants.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Implants;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Sunrise.Implants;

public abstract partial class SharedMindControlSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MindControlImplantComponent, AddImplantAttemptEvent>(OnAttemptImplant);
        SubscribeLocalEvent<MindControlImplantComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<MindControlImplantComponent, ImplantRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<MindControlComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnAttemptImplant(Entity<MindControlImplantComponent> ent, ref AddImplantAttemptEvent args)
    {
        if (args.User == args.Target)
        {
            args.Cancel();
            return;
        }

        if (!_mind.TryGetMind(args.Target, out _, out var targetMind) || _mind.IsCharacterDeadIc(targetMind))
        {
            _popup.PopupEntity(Loc.GetString("mind-control-invalid"), args.User, args.User, PopupType.Small);
            args.Cancel();
            return;
        }

        if (HasComp<MindShieldComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("mind-control-prevented"), args.User, args.User, PopupType.Small);
            args.Cancel();
            return;
        }

        ent.Comp.Master = args.User;
    }

    private void OnImplanted(Entity<MindControlImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        if (ent.Comp.Master is not { } master ||
            !_mind.TryGetMind(master, out _, out var masterMind) ||
            masterMind.CharacterName == null)
            return;

        if (!CanApplyMindControl(ent, args.Implanted, masterMind.CharacterName))
            return;

        EnsureComp<MindControlComponent>(args.Implanted);
        SendImplantedBriefing(ent, args.Implanted, masterMind.CharacterName);

        _status.TryAddStatusEffectDuration(args.Implanted, SleepingSystem.StatusEffectForcedSleeping, ent.Comp.ForcedSleepDuration);
        AssignObjective(args.Implanted);
    }

    private void OnRemoved(Entity<MindControlImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        if (TerminatingOrDeleted(args.Implanted))
            return;

        SendRemovedBriefing(ent, args.Implanted);
        RemoveObjective(args.Implanted);
        RemCompDeferred<MindControlComponent>(args.Implanted);
        _status.TryAddStatusEffectDuration(args.Implanted, SleepingSystem.StatusEffectForcedSleeping, ent.Comp.ForcedSleepDuration);
    }

    private void OnEmpPulse(Entity<MindControlComponent> ent, ref EmpPulseEvent args)
    {
        if (!TryComp<StaminaComponent>(ent.Owner, out var stamina))
            return;

        _stamina.TakeStaminaDamage(ent.Owner, stamina.CritThreshold, stamina);
        args.Affected = true;
        args.Disabled = true;
    }

    protected virtual bool CanApplyMindControl(Entity<MindControlImplantComponent> ent, EntityUid implanted, string masterName)
    {
        return true;
    }

    protected virtual void SendImplantedBriefing(Entity<MindControlImplantComponent> ent, EntityUid implanted, string masterName)
    {
    }

    protected virtual void SendRemovedBriefing(Entity<MindControlImplantComponent> ent, EntityUid implanted)
    {
    }

    protected virtual void AssignObjective(EntityUid uid)
    {
    }

    protected virtual void RemoveObjective(EntityUid uid)
    {
    }
}

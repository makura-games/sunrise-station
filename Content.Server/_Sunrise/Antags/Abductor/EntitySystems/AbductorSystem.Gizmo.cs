using Content.Shared._Sunrise.Antags.Abductor;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Starlight.Medical.Surgery;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.Antags.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;

    public void InitializeGizmo()
    {
        SubscribeLocalEvent<AbductorGizmoComponent, AfterInteractEvent>(OnGizmoInteract);
        SubscribeLocalEvent<AbductorGizmoComponent, MeleeHitEvent>(OnGizmoHitInteract);
        SubscribeLocalEvent<AbductorGizmoComponent, AbductorGizmoMarkDoAfterEvent>(OnGizmoDoAfter);
    }

    private void OnGizmoHitInteract(Entity<AbductorGizmoComponent> ent, ref MeleeHitEvent args)
    {
        if (args.Handled || !args.IsHit)
            return;

        if (args.HitEntities.Count != 1)
            return;

        if (TryStartMarkingTarget(ent, args.HitEntities[0], args.User))
            args.Handled = true;
    }

    private void OnGizmoInteract(Entity<AbductorGizmoComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        if (!args.CanReach)
            return;

        if (!_actionBlockerSystem.CanInstrumentInteract(args.User, args.Used, args.Target))
            return;

        if (TryComp<AbductorConsoleComponent>(target, out var console))
        {
            console.Target = ent.Comp.Target;
            Dirty(target, console);

            _popup.PopupEntity(Loc.GetString("abductors-ui-gizmo-transferred"), args.User);
            _color.RaiseEffect(Color.FromHex("#00BA00"), new List<EntityUid> { ent.Owner, target },
                Filter.Pvs(args.User, entityManager: EntityManager));

            UpdateGui(console.Target, (target, console));
            args.Handled = true;
            return;
        }

        if (TryStartMarkingTarget(ent, target, args.User))
            args.Handled = true;
    }

    private void OnGizmoDoAfter(Entity<AbductorGizmoComponent> ent, ref AbductorGizmoMarkDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!CanMarkTarget(target))
            return;

        DoMarkTarget(ent, target);
        args.Handled = true;
    }

    private bool TryStartMarkingTarget(Entity<AbductorGizmoComponent> gizmo, EntityUid target, EntityUid user)
    {
        if (!CanMarkTarget(target))
            return false;

        var doAfter = new DoAfterArgs(EntityManager, user, GetMarkDelay(gizmo, target),
                                        new AbductorGizmoMarkDoAfterEvent(),
                                        gizmo, target, gizmo.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1f,
            CancelDuplicate = false,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private bool CanMarkTarget(EntityUid target)
        => Exists(target) && HasComp<SurgeryTargetComponent>(target);

    private TimeSpan GetMarkDelay(Entity<AbductorGizmoComponent> gizmo, EntityUid target)
        => _tags.HasTag(target, gizmo.Comp.FastMarkTag) ? gizmo.Comp.FastMarkDelay : gizmo.Comp.MarkDelay;

    private void DoMarkTarget(Entity<AbductorGizmoComponent> gizmo, EntityUid target)
    {
        gizmo.Comp.Target = GetNetEntity(target);
        Dirty(gizmo);

        EnsureComp<AbductorVictimComponent>(target, out var victim);
        victim.LastActivation = _time.CurTime + gizmo.Comp.VictimActivationDelay;
        victim.Position ??= Transform(target).Coordinates;
        Dirty(target, victim);
    }
}

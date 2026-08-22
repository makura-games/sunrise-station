using Content.Server._Sunrise.CryoTeleport;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Utility;
using PaintDoAfterEvent = Content.Shared._Sunrise.Paint.PaintDoAfterEvent;
using Robust.Shared.Audio.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Content.Server.Emp;
using Robust.Shared.Timing;
using Content.Shared._Sunrise.Mech;
using Content.Shared.Coordinates;
using Content.Shared.Emp;
using Content.Shared.Damage.Systems;

namespace Content.Server._Sunrise.Mech;

/// <inheritdoc/>
public sealed partial class SunriseMechSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedMechSystem _mech = default!;



    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechPaintComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<MechPaintComponent, GetVerbsEvent<UtilityVerb>>(OnPaintVerb);
        SubscribeLocalEvent<MechVulnerableToEMPComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<MechPaintComponent, PaintDoAfterEvent>(OnPaint);
        SubscribeLocalEvent<MechPilotComponent, BeforeCryoTeleportEvent>(OnCryoTeleportAttemptEvent);
    }

    // Sunrise-start
    private void OnCryoTeleportAttemptEvent(EntityUid uid, MechPilotComponent component, BeforeCryoTeleportEvent args)
    {
        if (!TryComp<MechComponent>(component.Mech, out var mechComponent))
            return;
        _mech.TryEject(uid, mechComponent);
    }
    // Sunrise-end

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MechVulnerableToEMPComponent, MechOnEMPPulseComponent>();
        while (query.MoveNext(out var uid, out var comp, out var emp))
        {
            var curTime = _timing.CurTime;

            if (emp.NextEffectTime > curTime)
                continue;

            emp.NextEffectTime = curTime + emp.EffectInterval;

            SpawnAttachedTo(comp.EffectEMP, uid.ToCoordinates());

            if (curTime > comp.NextPulseTime)
                RemComp<MechOnEMPPulseComponent>(uid);
        }
    }

    private void OnEmpPulse(Entity<MechVulnerableToEMPComponent> ent, ref EmpPulseEvent args)
    {
        var curTime = _timing.CurTime;

        if (curTime < ent.Comp.NextPulseTime)
            return;

        ent.Comp.NextPulseTime = curTime + ent.Comp.CooldownTime;

        _damageable.TryChangeDamage(ent.Owner, ent.Comp.EmpDamage);

        EnsureComp<MechOnEMPPulseComponent>(ent);
    }

    private void OnPaintVerb(EntityUid uid, MechPaintComponent component, GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!HasComp<MechComponent>(args.Target))
            return;

        var paintText = Loc.GetString("paint-verb");

        var verb = new UtilityVerb()
        {
            Act = () =>
            {
                PrepPaint(uid, component, args.Target, args.User);
            },

            Text = paintText,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/paint.svg.192dpi.png"))
        };
        args.Verbs.Add(verb);
    }

    private void OnInteract(EntityUid uid, MechPaintComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (!Exists(args.Target))
            return;

        if (!HasComp<MechComponent>(args.Target))
            return;

        PrepPaint(uid, component, args.Target, args.User);
    }

    private void OnPaint(Entity<MechPaintComponent> entity, ref PaintDoAfterEvent args)
    {
        if (args.Used == null || !HasComp<MechComponent>(args.Target))
            return;

        if (args.Handled || args.Cancelled)
            return;

        if (args.Target is not { Valid: true } target)
            return;

        if (!TryComp<AppearanceComponent>(target, out var appearance))
            return;

        if (_openable.IsClosed(entity))
        {
            _popup.PopupEntity(Loc.GetString("paint-closed", ("used", args.Used)), args.User, args.User, PopupType.Medium);
            return;
        }

        if (_whitelistSystem.IsWhitelistFailOrNull(entity.Comp.Whitelist, target))
        {
            _popup.PopupEntity(Loc.GetString("paint-failure", ("target", args.Target)), args.User, args.User, PopupType.Medium);
            return;
        }

        if (entity.Comp.Used)
        {
            _popup.PopupEntity(Loc.GetString("paint-empty", ("used", args.Used)), args.User, args.User, PopupType.Medium);
            return;
        }

        _audio.PlayPvs(entity.Comp.Spray, entity);

        _popup.PopupEntity(Loc.GetString("paint-success", ("target", args.Target)), args.User, args.User, PopupType.Medium);

        entity.Comp.Used = true;
        args.Handled = true;

        _appearance.SetData(target, MechVisualLayers.Base, entity.Comp.BaseState, appearance);
        _appearance.SetData(target, MechVisualLayers.Open, entity.Comp.OpenState, appearance);
        _appearance.SetData(target, MechVisualLayers.Broken, entity.Comp.BrokenState, appearance);

    }

    private void PrepPaint(EntityUid uid, MechPaintComponent component, EntityUid? target, EntityUid user)
    {

        var doAfterEventArgs = new DoAfterArgs(EntityManager, user, component.Delay, new PaintDoAfterEvent(), uid, target: target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

}

using Content.Server.Chemistry.EntitySystems;
using Content.Shared._Sunrise.Paint;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.SubFloor;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.Paint;

/// <summary>
/// Colors target and consumes reagent on each color success.
/// </summary>
public sealed partial class PaintSystem : SharedPaintSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaintComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<PaintComponent, PaintDoAfterEvent>(OnPaint);
        SubscribeLocalEvent<PaintComponent, GetVerbsEvent<UtilityVerb>>(OnPaintVerb);
    }

    private void OnInteract(EntityUid uid, PaintComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (args.Target is not { Valid: true } target)
            return;

        PrepPaint(uid, component, target, args.User);
    }

    private void OnPaintVerb(EntityUid uid, PaintComponent component, GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
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
    private void PrepPaint(EntityUid uid, PaintComponent component, EntityUid target, EntityUid user)
    {

        var doAfterEventArgs = new DoAfterArgs(EntityManager, user, component.Delay, new PaintDoAfterEvent(), uid, target: target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true
        };

        if (!_doAfterSystem.TryStartDoAfter(doAfterEventArgs))
            return;
    }

    private void OnPaint(Entity<PaintComponent> entity, ref PaintDoAfterEvent args)
    {
        if (args.Target == null || args.Used == null)
            return;

        if (args.Handled || args.Cancelled)
            return;

        if (args.Target is not { Valid: true } target)
            return;

        if (_openable.IsClosed(entity))
        {
            _popup.PopupEntity(Loc.GetString("paint-closed", ("used", args.Used)), args.User, args.User, PopupType.Medium);
            return;
        }

        if (HasComp<SprayPaintedComponent>(target) || HasComp<RandomSpriteComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("paint-failure-painted", ("target", args.Target)), args.User, args.User, PopupType.Medium);
            return;
        }

        if (entity.Comp.Blacklist != null && _whitelist.IsValid(entity.Comp.Blacklist, target) || HasComp<HumanoidProfileComponent>(target) || HasComp<SubFloorHideComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("paint-failure", ("target", args.Target)), args.User, args.User, PopupType.Medium);
            return;
        }


        if (TryPaint(entity, target))
        {
            EnsureComp<SprayPaintedComponent>(target, out SprayPaintedComponent? paint);
            EnsureComp<AppearanceComponent>(target);

            paint.Color = entity.Comp.Color; // set the target color to the color specified in the spray paint yml.
            _audio.PlayPvs(entity.Comp.Spray, entity);
            paint.Enabled = true;

            if (HasComp<InventoryComponent>(target)) // Paint any clothing the target is wearing.
            {
                if (_inventory.TryGetSlots(target, out var slotDefinitions))
                {
                    foreach (var slot in slotDefinitions)
                    {
                        if (!_inventory.TryGetSlotEntity(target, slot.Name, out var slotEnt))
                            continue;

                        if (slotEnt == null)
                            return;

                        if (HasComp<SprayPaintedComponent>(slotEnt.Value) || entity.Comp.Blacklist != null &&
                                                                     _whitelist.IsValid(entity.Comp.Blacklist,
                                                                         slotEnt.Value)
                                                                     || HasComp<RandomSpriteComponent>(slotEnt.Value) ||
                                                                     HasComp<HumanoidProfileComponent>(
                                                                         slotEnt.Value))
                            return;

                        EnsureComp<SprayPaintedComponent>(slotEnt.Value, out SprayPaintedComponent? slotpaint);
                        EnsureComp<AppearanceComponent>(slotEnt.Value);
                        slotpaint.Color = entity.Comp.Color;
                        _appearanceSystem.SetData(slotEnt.Value, PaintVisuals.Painted, true);
                        Dirty(slotEnt.Value, slotpaint);
                    }
                }
            }

            _popup.PopupEntity(Loc.GetString("paint-success", ("target", args.Target)), args.User, args.User, PopupType.Medium);
            _appearanceSystem.SetData(target, PaintVisuals.Painted, true);
            Dirty(target, paint);
            args.Handled = true;
            return;
        }

        if (!TryPaint(entity, target))
        {
            _popup.PopupEntity(Loc.GetString("paint-empty", ("used", args.Used)), args.User, args.User, PopupType.Medium);
            return;
        }
    }

    private bool TryPaint(Entity<PaintComponent> reagent, EntityUid target)
    {
        if (HasComp<HumanoidProfileComponent>(target) || HasComp<SubFloorHideComponent>(target))
            return false;

        if (_solutionContainer.TryGetSolution(reagent.Owner, reagent.Comp.Solution, out var solutionEntity, out _))
        {
            var quantity = _solutionContainer.RemoveReagent(solutionEntity.Value, reagent.Comp.Reagent, reagent.Comp.ConsumptionUnit);
            if (quantity > 0)// checks quantity of solution is more than 0.
                return true;

            if (quantity < 1)
                return false;
        }
        return false;
    }
}

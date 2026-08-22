using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Sunrise.Paint;

/// <summary>
/// Removes paint from an entity.
/// </summary>
public sealed partial class PaintRemoverSystem : SharedPaintSystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaintRemoverComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<PaintRemoverComponent, PaintRemoverDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<PaintRemoverComponent, GetVerbsEvent<UtilityVerb>>(OnPaintRemoveVerb);
    }

    // When entity is painted, remove paint from that entity.
    private void OnInteract(EntityUid uid, PaintRemoverComponent component, AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (!args.CanReach || args.Target is not { Valid: true } target || !HasComp<SprayPaintedComponent>(target))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.CleanDelay, new PaintRemoverDoAfterEvent(), uid, args.Target, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnDropItem = true,
            MovementThreshold = 1.0f,
        });
        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, PaintRemoverComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (args.Target is not { Valid: true } target)
            return;

        if (!TryComp(target, out SprayPaintedComponent? paint))
            return;

        paint.Enabled = false;
        _audio.PlayPredicted(component.Sound, target, args.User);
        _popup.PopupClient(Loc.GetString("paint-removed", ("target", target)), args.User, args.User, PopupType.Medium);
        _appearanceSystem.SetData(target, PaintVisuals.Painted, false);
        RemComp<SprayPaintedComponent>(target);
        Dirty(target, paint);

        args.Handled = true;
    }

    private void OnPaintRemoveVerb(EntityUid uid, PaintRemoverComponent component, GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var paintremovalText = Loc.GetString("paint-remove-verb");

        var verb = new UtilityVerb()
        {
            Act = () => {

                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.CleanDelay, new PaintRemoverDoAfterEvent(), uid, args.Target, uid)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    BreakOnDropItem = true,
                    MovementThreshold = 1.0f,
                });
            },

            Text = paintremovalText
        };

        args.Verbs.Add(verb);
    }
}

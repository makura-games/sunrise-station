using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared._Sunrise.Smell;
using Content.Shared._Sunrise.Smell.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.Smell;

/// <summary>
/// Scent washing mechanic for items with ScentCleaningComponent (soap):
/// a right-click "Wash scents" verb on a scent bearer, a DoAfter, and on completion —
/// clearing the target's temporary scents and temporarily masking their base scent.
/// </summary>
public sealed partial class ScentCleaningSystem : EntitySystem
{
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SmellPrototypeCacheSystem _cache = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private ILocalizationManager _loc = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ScentCleaningComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
        SubscribeLocalEvent<ScentCleaningComponent, ScentCleaningDoAfterEvent>(OnScentCleaningDoAfter);
    }

    /// <summary>
    /// Right-click with soap in hand: show the "Wash scents" verb, but only
    /// if the target is a scent bearer (has ScentComponent).
    /// </summary>
    private void OnUtilityVerb(Entity<ScentCleaningComponent> cleaner, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!HasComp<ScentComponent>(args.Target))
            return;

        var user = args.User;
        var target = args.Target;

        args.Verbs.Add(new UtilityVerb
        {
            Act = () => TryCleanScents(cleaner.AsNullable(), user, (target, null)),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png")),
            Text = _loc.GetString("scent-cleaning-verb-text"),
            Message = _loc.GetString("scent-cleaning-verb-message"),
            DoContactInteraction = false,
        });
    }

    /// <summary>
    /// DoAfter finished: clear the target's temporary scents and apply temporary
    /// masking of the base scent. The event is directed at the cleaner (event target).
    /// </summary>
    private void OnScentCleaningDoAfter(Entity<ScentCleaningComponent> ent, ref ScentCleaningDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        if (!TryComp<ScentComponent>(args.Args.Target, out var scentComp))
            return;

        scentComp.TemporaryScents.Clear();
        scentComp.Masked = true;
        scentComp.MaskUntil = _timing.CurTime + ent.Comp.MaskDuration;
    }

    /// <summary>
    /// Public entry point for washing: validation via CanCleanScents, then execution.
    /// </summary>
    public bool TryCleanScents(Entity<ScentCleaningComponent?> cleaner, EntityUid user, Entity<ScentComponent?> target)
    {
        if (!Resolve(cleaner, ref cleaner.Comp, false))
            return false;

        if (!CanCleanScents(user, target))
            return false;

        DoCleanScents(cleaner, user, target);
        return true;
    }

    /// <summary>
    /// Checks whether scents can be washed off the target: the user must be
    /// able to interact, the target must carry scents and be within range.
    /// </summary>
    public bool CanCleanScents(EntityUid user, Entity<ScentComponent?> target)
    {
        if (!Resolve(target, ref target.Comp, false))
            return false;

        if (!_actionBlocker.CanInteract(user, target))
            return false;

        if (!_interaction.InRangeAndAccessible(user, (target, null),
                range: _cache.Config.ScentCleaningRange))
            return false;

        return true;
    }

    /// <summary>
    /// Performs the wash: start popup and DoAfter launch.
    /// </summary>
    private void DoCleanScents(Entity<ScentCleaningComponent?> cleaner, EntityUid user, EntityUid target)
    {
        _popup.PopupEntity(
            _loc.GetString("scent-cleaning-start", ("target", target)),
            user, user);

        var delay = cleaner.Comp!.CleanDelay;
        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay, new ScentCleaningDoAfterEvent(), cleaner, target: target, used: cleaner)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.01f,
            DistanceThreshold = _cache.Config.ScentCleaningRange,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }
}

// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt;

using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared._Sunrise.HairDye;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.HairDye;

/// <summary>
/// Система, обрабатывающая красители для волос.
/// </summary>
public sealed partial class HairDyeSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private SunriseHumanoidMarkingSystem _sunriseMarking = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HairDyerComponent, AfterInteractEvent>(OnHairDyerInteract);
        SubscribeLocalEvent<HairDyerComponent, HairDyeDoAfterEvent>(OnHairDyeDoAfter);
        SubscribeLocalEvent<HairDyerComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
    }

    /// <summary>
    /// Функция вызывается для переключения цели с бороды на волосы и наоборот
    /// </summary>
    private void OnGetVerbs(EntityUid uid, HairDyerComponent comp, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new InteractionVerb()
        {
            Text = comp.Mode ? Loc.GetString("hairdye-switch-hair") : Loc.GetString("hairdye-switch-facial"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => { comp.Mode = !comp.Mode; },
        });
    }

    /// <summary>
    /// Функция вызывается когда кто-то пытается применить краситель для волос
    /// </summary>
    private void OnHairDyerInteract(EntityUid uid, HairDyerComponent comp, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        var layer = comp.Mode ? HumanoidVisualLayers.FacialHair : HumanoidVisualLayers.Hair;
        if (!_sunriseMarking.TryGetLayerMarkings(target, layer, out var targetMarkings))
        {
            _popup.PopupEntity("Нельзя использовать краситель", args.User, args.User);
            return;
        }

        if (targetMarkings.Count == 0)
        {
            _popup.PopupEntity(comp.Mode ? "Нет лицевой растительности" : "Нет волос", args.User, args.User);
            return;
        }

        var currentColor = targetMarkings[0].MarkingColors.Count > 0
            ? targetMarkings[0].MarkingColors[0]
            : Color.White;
        var calculatedColor = Color.InterpolateBetween(currentColor, comp.TargetColor, 0.25f);

        var doAfterEvent = new HairDyeDoAfterEvent
        {
            TargetColor = calculatedColor,
        };

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(1), doAfterEvent, uid, target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnHairDyeDoAfter(EntityUid uid, HairDyerComponent comp, HairDyeDoAfterEvent args)
    {
        if (args.Handled || args.Target == null || args.Cancelled)
            return;

        var layer = comp.Mode ? HumanoidVisualLayers.FacialHair : HumanoidVisualLayers.Hair;
        _sunriseMarking.SetMarkingColor(args.Target.Value, layer, 0, [args.TargetColor]);
    }
}

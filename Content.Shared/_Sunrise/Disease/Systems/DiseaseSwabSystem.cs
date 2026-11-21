using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Взятие мазка: do-after, отметка об использовании и сообщения, одинаковые на клиенте и сервере.
/// </summary>
/// <remarks>
/// Мазок берётся с любой сущности, а не только с заражённых: иначе отказ на чистом предмете
/// выдавал бы его заражённость ещё до анализа. Что именно попало на мазок, система не знает:
/// в момент взятия она поднимает <see cref="DiseaseSwabTakenEvent"/>, а образец записывает
/// сервер — диагнозы живут только там.
/// </remarks>
public sealed partial class DiseaseSwabSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [SubscribeLocalEvent]
    private void OnAfterInteract(Entity<DiseaseSwabComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryStartSwab(ent, args.User, target))
            args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDoAfter(Entity<DiseaseSwabComponent> ent, ref DiseaseSwabDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (!CanSwab(ent, args.User, target))
            return;

        ent.Comp.Used = true;
        Dirty(ent);

        var taken = new DiseaseSwabTakenEvent(args.User, target);
        RaiseLocalEvent(ent.Owner, ref taken);

        _popup.PopupEntity(Loc.GetString("disease-swab-popup-taken", ("target", target)), target, args.User);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<DiseaseSwabComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Used ? "disease-swab-examine-used" : "disease-swab-examine-unused"));
    }

    /// <summary>
    /// Начинает взятие образца с цели.
    /// </summary>
    /// <returns>True, если do-after запущен.</returns>
    public bool TryStartSwab(Entity<DiseaseSwabComponent> swab, EntityUid user, EntityUid target)
    {
        if (!CanSwab(swab, user, target, quiet: false))
            return false;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, swab.Comp.SwabDelay, new DiseaseSwabDoAfterEvent(), swab, target: target, used: swab)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    /// Проверяет, можно ли взять мазком образец с цели.
    /// </summary>
    public bool CanSwab(Entity<DiseaseSwabComponent> swab, EntityUid user, EntityUid target, bool quiet = true)
    {
        if (target == swab.Owner)
            return false;

        if (!swab.Comp.Used)
            return true;

        if (!quiet)
            _popup.PopupEntity(Loc.GetString("disease-swab-popup-used"), swab, user);

        return false;
    }
}

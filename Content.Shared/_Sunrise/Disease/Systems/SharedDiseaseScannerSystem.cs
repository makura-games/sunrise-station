using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Ручной анализатор: заправка бумагой, запуск осмотра и расход листа предсказываются на клиенте.
/// </summary>
/// <remarks>
/// Содержимое отчёта знает только сервер, поэтому сам лист печатается в <see cref="OnScanned"/>.
/// </remarks>
public abstract partial class SharedDiseaseScannerSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private EntityQuery<PaperComponent> _paperQuery = default!;

    [SubscribeLocalEvent]
    private void OnAfterInteract(Entity<DiseaseScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryStartScan(ent, args.User, target))
            args.Handled = true;
    }

    /// <summary>
    /// Заправка: лист бумаги уходит в прибор.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<DiseaseScannerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_paperQuery.HasComp(args.Used))
            return;

        args.Handled = true;
        if (ent.Comp.Paper >= ent.Comp.MaxPaper)
        {
            _popup.PopupEntity(Loc.GetString("disease-scanner-popup-full"), ent, args.User);
            return;
        }

        ent.Comp.Paper++;
        Dirty(ent);
        PredictedQueueDel(args.Used);
        _popup.PopupEntity(Loc.GetString("disease-scanner-popup-reloaded", ("paper", ent.Comp.Paper)), ent, args.User);
    }

    [SubscribeLocalEvent]
    private void OnDoAfter(Entity<DiseaseScannerComponent> ent, ref DiseaseScanDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (!CanScan(ent, args.User))
            return;

        ent.Comp.Paper--;
        Dirty(ent);

        _audio.PlayPredicted(ent.Comp.PrintSound, ent.Owner, args.User);
        OnScanned(ent, args.User, target);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<DiseaseScannerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("disease-scanner-examine", ("paper", ent.Comp.Paper), ("max", ent.Comp.MaxPaper)));
    }

    /// <summary>
    /// Начинает осмотр цели.
    /// </summary>
    /// <returns>True, если do-after запущен.</returns>
    public bool TryStartScan(Entity<DiseaseScannerComponent> scanner, EntityUid user, EntityUid target)
    {
        if (!CanScan(scanner, user, quiet: false))
            return false;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            scanner.Comp.ScanDelay,
            new DiseaseScanDoAfterEvent(),
            scanner,
            target: target,
            used: scanner)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    /// Проверяет, может ли прибор печатать.
    /// </summary>
    public bool CanScan(Entity<DiseaseScannerComponent> scanner, EntityUid user, bool quiet = true)
    {
        if (scanner.Comp.Paper > 0)
            return true;

        if (!quiet)
            _popup.PopupEntity(Loc.GetString("disease-scanner-popup-empty"), scanner, user);

        return false;
    }

    /// <summary>
    /// Серверная часть: составить отчёт и выдать распечатку. Лист уже списан.
    /// </summary>
    protected virtual void OnScanned(Entity<DiseaseScannerComponent> scanner, EntityUid user, EntityUid target)
    {
    }
}

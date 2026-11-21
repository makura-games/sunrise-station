using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Одноразовые вакцины и сыворотки: старт укола и исчезновение использованной ампулы предсказываются.
/// </summary>
/// <remarks>
/// Подействовал ли препарат, решает сервер в <see cref="OnInjected"/>: болезни клиенту не отдаются.
/// </remarks>
public abstract partial class SharedDiseaseVaccineSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;

    [SubscribeLocalEvent]
    private void OnAfterInteract(Entity<DiseaseVaccineComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryStartInjection(ent, args.User, target))
            args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDoAfter(Entity<DiseaseVaccineComponent> ent, ref DiseaseVaccineDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (!CanInject(ent, args.User, target))
            return;

        OnInjected(ent, args.User, target);
        PredictedQueueDel(ent.Owner);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<DiseaseVaccineComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(IsLoaded(ent.Comp)
            ? Loc.GetString("disease-vaccine-examine-loaded", ("strain", ent.Comp.StrainCode))
            : Loc.GetString("disease-vaccine-examine-empty"));
    }

    /// <summary>
    /// Начинает инъекцию препарата в цель.
    /// </summary>
    /// <returns>True, если do-after запущен.</returns>
    public bool TryStartInjection(Entity<DiseaseVaccineComponent> vaccine, EntityUid user, EntityUid target)
    {
        if (!CanInject(vaccine, user, target, quiet: false))
            return false;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, vaccine.Comp.InjectDelay, new DiseaseVaccineDoAfterEvent(), vaccine, target: target, used: vaccine)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    /// Проверяет, можно ли ввести препарат цели.
    /// </summary>
    public bool CanInject(Entity<DiseaseVaccineComponent> vaccine, EntityUid user, EntityUid target, bool quiet = true)
    {
        // Клики по посторонним сущностям молча игнорируются.
        if (!_mobStateQuery.HasComp(target))
            return false;

        if (IsLoaded(vaccine.Comp))
            return true;

        if (!quiet)
            _popup.PopupEntity(Loc.GetString("disease-vaccine-popup-empty"), vaccine, user);

        return false;
    }

    /// <summary>
    /// Заряжен ли препарат. Видно по коду штамма на этикетке.
    /// </summary>
    public static bool IsLoaded(DiseaseVaccineComponent vaccine)
    {
        return !string.IsNullOrEmpty(vaccine.StrainCode);
    }

    /// <summary>
    /// Серверная часть: действие препарата на цель. Ампулу после этого удаляет общий код.
    /// </summary>
    protected virtual void OnInjected(Entity<DiseaseVaccineComponent> vaccine, EntityUid user, EntityUid target)
    {
    }
}

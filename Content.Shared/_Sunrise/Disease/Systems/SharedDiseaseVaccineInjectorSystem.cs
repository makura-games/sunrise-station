using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Многоразовый вакцинный инжектор: старт укола и проверка заряда предсказываются на клиенте.
/// </summary>
/// <remarks>
/// Вакцина в слоте не расходуется. Её действие на цель считает сервер в <see cref="OnInjected"/>.
/// </remarks>
public abstract partial class SharedDiseaseVaccineInjectorSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private EntityQuery<DiseaseVaccineComponent> _vaccineQuery = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;

    [SubscribeLocalEvent]
    private void OnAfterInteract(Entity<DiseaseVaccineInjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryStartInjection(ent, args.User, target))
            args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDoAfter(Entity<DiseaseVaccineInjectorComponent> ent, ref DiseaseVaccineInjectorDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (!CanInject(ent, args.User, target) || !TryGetVaccine(ent, out var vaccine))
            return;

        OnInjected(vaccine, args.User, target);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<DiseaseVaccineInjectorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(TryGetVaccine(ent, out var vaccine)
            ? Loc.GetString("disease-vaccine-injector-examine-loaded", ("strain", vaccine.Comp.StrainCode))
            : Loc.GetString("disease-vaccine-injector-examine-empty"));
    }

    /// <summary>
    /// Начинает укол цели вакциной из слота.
    /// </summary>
    /// <returns>True, если do-after запущен.</returns>
    public bool TryStartInjection(Entity<DiseaseVaccineInjectorComponent> injector, EntityUid user, EntityUid target)
    {
        if (!CanInject(injector, user, target, quiet: false))
            return false;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            injector.Comp.InjectDelay,
            new DiseaseVaccineInjectorDoAfterEvent(),
            injector,
            target: target,
            used: injector)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    /// Проверяет, можно ли уколоть цель: это живое существо, а в инжекторе заряженная вакцина.
    /// </summary>
    public bool CanInject(Entity<DiseaseVaccineInjectorComponent> injector, EntityUid user, EntityUid target, bool quiet = true)
    {
        // Клики по посторонним сущностям молча игнорируются.
        if (!_mobStateQuery.HasComp(target))
            return false;

        if (TryGetVaccine(injector, out _))
            return true;

        if (!quiet)
            _popup.PopupEntity(Loc.GetString("disease-vaccine-injector-popup-empty"), injector, user);

        return false;
    }

    /// <summary>
    /// Заряженная вакцина в слоте инжектора.
    /// </summary>
    public bool TryGetVaccine(Entity<DiseaseVaccineInjectorComponent> injector, out Entity<DiseaseVaccineComponent> vaccine)
    {
        vaccine = default;
        if (_itemSlots.GetItemOrNull(injector.Owner, injector.Comp.SlotId) is not { } item ||
            !_vaccineQuery.TryComp(item, out var comp) ||
            !SharedDiseaseVaccineSystem.IsLoaded(comp))
        {
            return false;
        }

        vaccine = (item, comp);
        return true;
    }

    /// <summary>
    /// Серверная часть: действие вакцины на цель. Вакцина остаётся в слоте.
    /// </summary>
    protected virtual void OnInjected(Entity<DiseaseVaccineComponent> vaccine, EntityUid user, EntityUid target)
    {
    }
}

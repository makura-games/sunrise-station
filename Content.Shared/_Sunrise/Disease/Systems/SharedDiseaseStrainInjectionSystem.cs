using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.Body.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Укол выведенным штаммом: старт do-after, сообщение и расход пробирки предсказываются.
/// </summary>
/// <remarks>
/// Подхватит ли цель болезнь, решает сервер в <see cref="OnInjected"/> уже после укола. Раньше отказ
/// на старте выдавал иммунитет цели, поэтому теперь доза тратится в любом случае.
/// </remarks>
public abstract partial class SharedDiseaseStrainInjectionSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private EntityQuery<BloodstreamComponent> _bloodstreamQuery = default!;
    [Dependency] private EntityQuery<DiseaseStrainSampleComponent> _strainQuery = default!;

    [SubscribeLocalEvent]
    private void OnAfterInteract(Entity<DiseaseStrainInjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryStartInjection(ent, args.User, target))
            args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDoAfter(Entity<DiseaseStrainInjectorComponent> ent, ref DiseaseStrainInjectDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!CanInject(ent, target))
            return;

        OnInjected(ent, args.User, target);
        _popup.PopupEntity(Loc.GetString("disease-strain-injected"), target, args.User);
        PredictedQueueDel(ent.Owner);
        args.Handled = true;
    }

    /// <summary>
    /// Начинает укол, если в пробирке есть штамм, а у цели — кровоток.
    /// </summary>
    /// <returns>True, если do-after запущен.</returns>
    public bool TryStartInjection(Entity<DiseaseStrainInjectorComponent> ent, EntityUid user, EntityUid target)
    {
        if (!CanInject(ent, target))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.InjectDelay,
            new DiseaseStrainInjectDoAfterEvent(), ent.Owner, target: target, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    /// <summary>
    /// Проверяет то, что видно и без анализа: в пробирке штамм, у цели есть кровь.
    /// </summary>
    public bool CanInject(Entity<DiseaseStrainInjectorComponent> ent, EntityUid target)
    {
        return !TerminatingOrDeleted(ent) &&
               _bloodstreamQuery.HasComp(target) &&
               _strainQuery.TryComp(ent, out var sample) &&
               sample.HasStrain;
    }

    /// <summary>
    /// Серверная часть: попытка заразить цель. Пробирку после этого удаляет общий код.
    /// </summary>
    protected virtual void OnInjected(Entity<DiseaseStrainInjectorComponent> ent, EntityUid user, EntityUid target)
    {
    }
}

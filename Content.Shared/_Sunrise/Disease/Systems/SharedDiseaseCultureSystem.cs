using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Засев чашки Петри: старт do-after, отметка о засеве, вид чашки и расход пробирки предсказываются.
/// </summary>
/// <remarks>
/// Засевать можно любым использованным мазком, в том числе чистым: отказ на чистом мазке выдавал бы
/// результат анализа раньше времени. Чистая чашка просто не прорастёт. Какой штамм попал в чашку
/// и как она растёт, решает сервер в <see cref="OnInoculated"/>.
/// </remarks>
public abstract partial class SharedDiseaseCultureSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private EntityQuery<DiseaseStrainSampleComponent> _strainQuery = default!;
    [Dependency] private EntityQuery<DiseaseSwabComponent> _swabQuery = default!;

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<DiseaseCultureComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartInoculation(ent, args.User, args.Used))
            args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDoAfter(Entity<DiseaseCultureComponent> ent, ref DiseaseCultureDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used)
            return;

        if (!CanInoculate(ent, args.User, used))
            return;

        ent.Comp.Inoculated = true;
        Dirty(ent);
        _appearance.SetData(ent.Owner, DiseaseCultureVisuals.State, DiseaseCultureState.Growing);

        OnInoculated(ent, args.User, used);

        // Пробирка расходуется, мазок остаётся: его ещё можно отнести в диагностер.
        if (_strainQuery.HasComp(used))
            PredictedQueueDel(used);

        _popup.PopupEntity(Loc.GetString("disease-culture-popup-seeded"), ent, args.User);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<DiseaseCultureComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var key = !ent.Comp.Inoculated ? "disease-culture-examine-empty"
            : !ent.Comp.Ready ? "disease-culture-examine-growing"
            : ent.Comp.HasColonies ? "disease-culture-examine-ready"
            : "disease-culture-examine-sterile";

        args.PushMarkup(Loc.GetString(key));
    }

    /// <summary>
    /// Начинает засев чашки мазком или пробиркой со штаммом.
    /// </summary>
    /// <returns>True, если do-after запущен.</returns>
    public bool TryStartInoculation(Entity<DiseaseCultureComponent> dish, EntityUid user, EntityUid used)
    {
        if (!CanInoculate(dish, user, used, quiet: false))
            return false;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            dish.Comp.InoculateDelay,
            new DiseaseCultureDoAfterEvent(),
            dish,
            target: dish,
            used: used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    /// Проверяет, можно ли засеять чашку этим предметом.
    /// </summary>
    public bool CanInoculate(Entity<DiseaseCultureComponent> dish, EntityUid user, EntityUid used, bool quiet = true)
    {
        // Посторонние предметы молча игнорируются: иначе чашкой нельзя было бы пользоваться как предметом.
        if (!IsInoculant(used))
            return false;

        if (!dish.Comp.Inoculated)
            return true;

        if (!quiet)
            _popup.PopupEntity(Loc.GetString("disease-culture-popup-used"), dish, user);

        return false;
    }

    /// <summary>
    /// Годится ли предмет для засева: использованный мазок или пробирка со штаммом.
    /// </summary>
    public bool IsInoculant(EntityUid used)
    {
        if (_swabQuery.TryComp(used, out var swab))
            return swab.Used;

        return _strainQuery.TryComp(used, out var strain) && strain.HasStrain;
    }

    /// <summary>
    /// Серверная часть: перенести штамм на чашку и запустить рост.
    /// </summary>
    protected virtual void OnInoculated(Entity<DiseaseCultureComponent> dish, EntityUid user, EntityUid used)
    {
    }
}

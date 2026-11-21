using Content.Server._Sunrise.Disease.Components;
using Content.Server.Popups;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Дезинфектант убивает заразу там, куда попал.
/// </summary>
/// <remarks>
/// Работает от реагента, а не от прибора: распылитель — обычный пульверизатор, как антисорняк,
/// поэтому обеззараживает всё, чего дезинфектант коснулся, — облако из распылителя, выплеснутый
/// стакан, смоченная тряпка. Живого больного это не лечит, его дело — вакцина; снаружи достаются
/// только предметы-очаги и мёртвое тело. Граната и стерилизующая арка пользуются тем же API.
/// </remarks>
public sealed partial class DiseaseDisinfectantSystem : EntitySystem
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>Кеш прототипы всех видов реагентов дезинфектантов. Выглядит не очень,
    /// но второй вариант - добавить в прототип реагента еще одно поле - кажется мне
    /// еще хуже.</summary>
    private readonly Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> _disinfectants = new();

    private readonly HashSet<Entity<DiseaseSourceComponent>> _sources = new();
    private readonly HashSet<Entity<DiseaseCarrierComponent>> _carriers = new();

    [SubscribeLocalEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _disinfectants.Clear();
    }

    /// <summary>
    /// Предмет-очаг перестаёт быть заразным.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnSourceReaction(Entity<DiseaseSourceComponent> ent, ref ReactionEntityEvent args)
    {
        if (IsDisinfectant(ref args))
            TryDisinfect(ent.Owner);
    }

    /// <summary>
    /// С мёртвого тела заразу снять можно, с живого — нет. Надетое и то, что в руках, обрабатывается
    /// в обоих случаях: так отмывают загрязнённые перчатки, не снимая их.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnMobReaction(Entity<MobStateComponent> ent, ref ReactionEntityEvent args)
    {
        if (!IsDisinfectant(ref args))
            return;

        TryDisinfect(ent.Owner);
        DisinfectCarried(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnTrigger(Entity<DiseaseDisinfectOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        DisinfectArea(_transform.GetMapCoordinates(ent.Owner), ent.Comp.Range);
        args.Handled = true;
    }

    /// <summary>
    /// Гасит заразу на сущности: снимает очаг с предмета или болезни с мёртвого тела.
    /// </summary>
    /// <param name="target">Обрабатываемая сущность.</param>
    /// <param name="quiet">Не показывать всплывающее сообщение на самой сущности.</param>
    /// <returns>True, если на сущности было что обеззараживать.</returns>
    public bool TryDisinfect(EntityUid target, bool quiet = false)
    {
        if (HasComp<DiseaseSourceComponent>(target))
        {
            RemComp<DiseaseSourceComponent>(target);

            // Обработанный нарост гаснет целиком: разбитый после дезинфекции, он уже не выпускает облако.
            RemComp<DiseaseCloudOnTriggerComponent>(target);

            if (!quiet)
                _popup.PopupEntity(Loc.GetString("disease-disinfectant-popup-cleaned"), target);

            return true;
        }

        if (!HasComp<DiseaseCarrierComponent>(target) || !_mobState.IsDead(target))
            return false;

        _disease.CureAll(target, DiseaseCureReason.Treatment);
        if (!quiet)
            _popup.PopupEntity(Loc.GetString("disease-disinfectant-popup-body"), target);

        return true;
    }

    /// <summary>
    /// Обеззараживает всё, что сущность держит в руках и носит на себе.
    /// </summary>
    /// <returns>Сколько предметов удалось обработать.</returns>
    public int DisinfectCarried(EntityUid holder)
    {
        var cleaned = 0;
        foreach (var item in _inventory.GetHandOrInventoryEntities(holder))
        {
            if (TryDisinfect(item, quiet: true))
                cleaned++;
        }

        return cleaned;
    }

    /// <summary>
    /// Обеззараживает очаги и мёртвые тела в радиусе.
    /// </summary>
    /// <returns>Сколько сущностей удалось обработать.</returns>
    public int DisinfectArea(MapCoordinates coordinates, float range)
    {
        var cleaned = 0;

        _sources.Clear();
        _lookup.GetEntitiesInRange(coordinates, range, _sources);
        foreach (var source in _sources)
        {
            if (TryDisinfect(source.Owner))
                cleaned++;
        }

        _carriers.Clear();
        _lookup.GetEntitiesInRange(coordinates, range, _carriers);
        foreach (var carrier in _carriers)
        {
            if (TryDisinfect(carrier.Owner))
                cleaned++;
        }

        return cleaned;
    }

    /// <summary>
    /// Считается ли попавший реагент дезинфектантом и хватило ли его.
    /// </summary>
    private bool IsDisinfectant(ref ReactionEntityEvent args)
    {
        // Съеденный дезинфектант очаг не чистит: заразу убивает обработка поверхности, а не желудок.
        if (args.Method != ReactionMethod.Touch)
            return false;

        EnsureDisinfectants();
        return _disinfectants.TryGetValue(args.Reagent.ID, out var minAmount)
               && args.ReagentQuantity.Quantity >= minAmount;
    }

    private void EnsureDisinfectants()
    {
        if (_disinfectants.Count > 0)
            return;

        foreach (var proto in _proto.EnumeratePrototypes<DiseaseDisinfectantPrototype>())
        {
            _disinfectants[proto.Reagent] = proto.MinAmount;
        }
    }
}

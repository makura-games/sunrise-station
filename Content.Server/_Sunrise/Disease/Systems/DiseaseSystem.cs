using System.Diagnostics.CodeAnalysis;
using Content.Server._Sunrise.Disease.Components;
using Content.Server.Administration.Logs;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Единственный владелец состояния болезней: заражение, развитие, распространение, лечение и иммунитет.
/// Остальные системы меняют состояние болезней только через публичный API этой системы.
/// </summary>
/// <remarks>
/// Только сервер. Состояние болезней не сетевится; клиент видит лишь последствия симптомов.
/// </remarks>
public sealed partial class DiseaseSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    [Dependency] private EntityQuery<DiseaseCarrierComponent> _carrierQuery = default!;
    [Dependency] private EntityQuery<DiseaseHostComponent> _hostQuery = default!;
    [Dependency] private EntityQuery<InventoryComponent> _inventoryQuery = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeSpread();
    }

    [SubscribeLocalEvent]
    private void OnRejuvenate(Entity<DiseaseCarrierComponent> ent, ref RejuvenateEvent args)
    {
        CureAll(ent.AsNullable(), DiseaseCureReason.Rejuvenate);
    }

    #region Infection

    /// <summary>
    /// Пытается заразить сущность по указанному пути передачи.
    /// Проверяет, может ли цель заболеть, затем бросает <paramref name="chance"/> с учётом защиты цели.
    /// </summary>
    /// <param name="target">Сущность, подвергшаяся воздействию.</param>
    /// <param name="disease">Болезнь, которой подвергается цель.</param>
    /// <param name="route">Путь передачи. Болезнь должна его поддерживать.</param>
    /// <param name="source">Заражённый носитель или предмет, если есть.</param>
    /// <param name="chance">Шанс заражения до учёта защиты.</param>
    /// <param name="contact">Чем произошёл контакт: от этого зависит, какая экипировка защищает.</param>
    /// <param name="ignoreRoutes">
    /// Заражать, даже если штамм не умеет передаваться этим путём. Для аварий, когда наружу
    /// вырывается лабораторная культура; защита по <paramref name="route"/> всё равно действует.
    /// </param>
    /// <returns>True, если цель заразилась.</returns>
    public bool TryInfect(
        EntityUid target,
        EntityUid disease,
        DiseaseTransmissionRoute route,
        EntityUid? source = null,
        float chance = 1f,
        DiseaseContactKind contact = DiseaseContactKind.Touch,
        bool ignoreRoutes = false)
    {
        if (!CanInfect(target, disease, route, ignoreRoutes))
            return false;

        var multiplier = GetExposureMultiplier(target, disease, route, source, contact);
        if (!_random.Prob(Math.Clamp(chance * multiplier, 0f, 1f)))
            return false;

        return Infect(target, disease, route, source);
    }

    /// <summary>
    /// Проверяет, может ли сущность заразиться болезнью по указанному пути, без учёта защиты и шанса.
    /// </summary>
    /// <remarks>
    /// Первой стоит проверка <see cref="DiseaseHostComponent"/>: кто болеть не умеет,
    /// тот отсекается до всей прочей работы. Умышленное заражение через <see cref="Infect"/>
    /// эту проверку не проходит и работает на любом мобе.
    /// </remarks>
    /// <param name="ignoreRoutes">Не требовать, чтобы штамм поддерживал путь <paramref name="route"/>.</param>
    public bool CanInfect(EntityUid target, EntityUid disease, DiseaseTransmissionRoute route, bool ignoreRoutes = false)
    {
        if (!_hostQuery.HasComp(target))
            return false;

        if (!_registry.TryGetData(disease, out var proto))
            return false;

        if (route == DiseaseTransmissionRoute.None || !ignoreRoutes && (proto.Routes & route) != route)
            return false;

        if (!_mobStateQuery.HasComp(target) || _mobState.IsDead(target))
            return false;

        if (!_whitelist.CheckBoth(target, proto.Blacklist, proto.Whitelist))
            return false;

        return !HasDisease(target, disease) && !HasImmunity(target, disease);
    }

    /// <summary>
    /// Собирает множитель защиты от воздействия с самой цели и её надетой экипировки.
    /// </summary>
    /// <returns>Множитель шанса заражения. Ноль, если воздействие заблокировано.</returns>
    public float GetExposureMultiplier(
        EntityUid target,
        EntityUid disease,
        DiseaseTransmissionRoute route,
        EntityUid? source = null,
        DiseaseContactKind contact = DiseaseContactKind.Touch)
    {
        var ev = new DiseaseExposureAttemptEvent(disease, route, source, contact);
        RaiseLocalEvent(target, ref ev);

        // Надетая экипировка (маски, перчатки) получает то же событие через релей инвентаря.
        if (_inventoryQuery.TryComp(target, out var inventory))
            _inventory.RelayEvent((target, inventory), ref ev);

        return ev.Cancelled ? 0f : MathF.Max(ev.Multiplier, 0f);
    }

    /// <summary>
    /// Заражает сущность без проверок и бросков шанса. Для игровых заражений используйте <see cref="TryInfect"/>.
    /// </summary>
    /// <param name="route"><see cref="DiseaseTransmissionRoute.None"/> для принудительных заражений, например админских.</param>
    /// <returns>False, если болезнь некорректна или сущность уже ею больна.</returns>
    public bool Infect(
        EntityUid target,
        EntityUid disease,
        DiseaseTransmissionRoute route = DiseaseTransmissionRoute.None,
        EntityUid? source = null)
    {
        if (!_registry.TryGetData(disease, out var proto))
            return false;

        if (proto.Stages.Count == 0)
        {
            Log.Error($"Disease {ToPrettyString(disease)} has no stages and cannot infect {ToPrettyString(target)}");
            return false;
        }

        if (HasDisease(target, disease))
            return false;

        var carrier = EnsureComp<DiseaseCarrierComponent>(target);
        if (carrier.Diseases.Count == 0)
            carrier.NextUpdate = _timing.CurTime + UpdateInterval;

        var instance = new DiseaseInstance(disease, _timing.CurTime);
        carrier.Diseases.Add(instance);

        if (source is { } sourceUid)
        {
            _adminLog.Add(LogType.Disease, LogImpact.Medium,
                $"{ToPrettyString(target):target} was infected with {proto.StrainCode} via {route} from {ToPrettyString(sourceUid):source}");
        }
        else
        {
            _adminLog.Add(LogType.Disease, LogImpact.Medium,
                $"{ToPrettyString(target):target} was infected with {proto.StrainCode} via {route}");
        }

        var ev = new DiseaseInfectedEvent(disease, route, source);
        RaiseLocalEvent(target, ref ev);

        EnterStage(target, instance, proto);
        return true;
    }

    #endregion

    #region Treatment

    /// <summary>
    /// Запускает постепенное выздоровление: болезнь откатывается по стадиям, теряя симптомы, пока не исчезнет.
    /// </summary>
    /// <param name="reason"><see cref="DiseaseCureReason.Treatment"/> и <see cref="DiseaseCureReason.Natural"/> в конце дают иммунитет.</param>
    /// <returns>False, если носитель не болен этой болезнью или она уже отступает.</returns>
    public bool TryBeginRecovery(Entity<DiseaseCarrierComponent?> ent, EntityUid disease, DiseaseCureReason reason)
    {
        if (!TryGetDisease(ent, disease, out var instance) || instance.Recovering)
            return false;

        BeginRecovery(ent.Owner, instance, reason);
        return true;
    }

    /// <summary>
    /// Мгновенно удаляет болезнь у носителя, минуя постепенное выздоровление.
    /// </summary>
    /// <returns>False, если носитель не болен этой болезнью.</returns>
    public bool TryCure(Entity<DiseaseCarrierComponent?> ent, EntityUid disease, DiseaseCureReason reason)
    {
        if (!_carrierQuery.Resolve(ent, ref ent.Comp, false))
            return false;

        var index = IndexOf(ent.Comp, disease);
        if (index < 0)
            return false;

        Cure((ent.Owner, ent.Comp), index, reason);
        return true;
    }

    /// <summary>
    /// Мгновенно удаляет все болезни носителя.
    /// </summary>
    public void CureAll(Entity<DiseaseCarrierComponent?> ent, DiseaseCureReason reason)
    {
        if (!_carrierQuery.Resolve(ent, ref ent.Comp, false))
            return;

        for (var i = ent.Comp.Diseases.Count - 1; i >= 0; i--)
        {
            Cure((ent.Owner, ent.Comp), i, reason);
        }
    }

    /// <summary>
    /// Добавляет прогресс лечения подходящим болезням. Болезни, достигшие порога, начинают отступать.
    /// </summary>
    /// <param name="amount">Добавляемый прогресс.</param>
    /// <param name="diseases">Целевые болезни. Приоритетнее <paramref name="classification"/>.</param>
    /// <param name="classification">Целевой класс болезней. Если оба фильтра пусты, затрагиваются все болезни.</param>
    public void AddCureProgress(
        Entity<DiseaseCarrierComponent?> ent,
        float amount,
        List<ProtoId<DiseasePrototype>>? diseases = null,
        DiseaseClassification? classification = null)
    {
        if (amount <= 0f || !_carrierQuery.Resolve(ent, ref ent.Comp, false))
            return;

        // Индексный обход: подписчики DiseaseRecoveryStartedEvent могут менять список болезней.
        for (var i = ent.Comp.Diseases.Count - 1; i >= 0; i--)
        {
            if (i >= ent.Comp.Diseases.Count)
                continue;

            var instance = ent.Comp.Diseases[i];
            if (instance.Recovering ||
                !_registry.TryGetData(instance.Disease, out var proto) ||
                !MatchesFilter(instance.Disease, proto, diseases, classification))
            {
                continue;
            }

            AddProgress(ent.Owner, instance, proto, amount, DiseaseCureReason.Treatment);
        }
    }

    /// <summary>
    /// Доливает прогресс лечения одной болезни и запускает выздоровление, если набран порог.
    /// </summary>
    /// <param name="reason">Причина выздоровления: от неё зависит, выдадут ли иммунитет.</param>
    private void AddProgress(
        EntityUid uid,
        DiseaseInstance instance,
        DiseaseDefinitionComponent proto,
        float amount,
        DiseaseCureReason reason)
    {
        instance.CureProgress += amount;
        if (instance.CureProgress >= proto.CureThreshold)
            BeginRecovery(uid, instance, reason);
    }

    /// <summary>
    /// Добавляет время снятия симптомов подходящим болезням.
    /// В отличие от подавления, не влияет ни на развитие болезни, ни на её заразность.
    /// </summary>
    /// <param name="duration">Добавляемое время облегчения.</param>
    /// <param name="maxDuration">Верхний предел накопленного времени облегчения.</param>
    /// <param name="diseases">Целевые болезни. Приоритетнее <paramref name="classification"/>.</param>
    /// <param name="classification">Целевой класс болезней. Если оба фильтра пусты, затрагиваются все болезни.</param>
    /// <returns>True, если симптомы сняты хотя бы у одной болезни.</returns>
    public bool TryRelieve(
        Entity<DiseaseCarrierComponent?> ent,
        TimeSpan duration,
        TimeSpan maxDuration,
        List<ProtoId<DiseasePrototype>>? diseases = null,
        DiseaseClassification? classification = null)
    {
        if (duration <= TimeSpan.Zero || !_carrierQuery.Resolve(ent, ref ent.Comp, false))
            return false;

        var relieved = false;
        foreach (var instance in ent.Comp.Diseases)
        {
            if (!_registry.TryGetData(instance.Disease, out var proto) ||
                !MatchesFilter(instance.Disease, proto, diseases, classification))
            {
                continue;
            }

            instance.ReliefRemaining = Accumulate(instance.ReliefRemaining, duration, maxDuration);
            relieved = true;
        }

        return relieved;
    }

    /// <summary>
    /// Добавляет время подавления подходящим болезням.
    /// </summary>
    /// <param name="duration">Добавляемое время подавления.</param>
    /// <param name="maxDuration">Верхний предел накопленного времени подавления.</param>
    /// <param name="diseases">Целевые болезни. Приоритетнее <paramref name="classification"/>.</param>
    /// <param name="classification">Целевой класс болезней. Если оба фильтра пусты, затрагиваются все болезни.</param>
    /// <returns>True, если подавлена хотя бы одна болезнь.</returns>
    public bool TrySuppress(
        Entity<DiseaseCarrierComponent?> ent,
        TimeSpan duration,
        TimeSpan maxDuration,
        List<ProtoId<DiseasePrototype>>? diseases = null,
        DiseaseClassification? classification = null)
    {
        if (duration <= TimeSpan.Zero || !_carrierQuery.Resolve(ent, ref ent.Comp, false))
            return false;

        var suppressed = false;
        foreach (var instance in ent.Comp.Diseases)
        {
            if (!_registry.TryGetData(instance.Disease, out var proto) ||
                !MatchesFilter(instance.Disease, proto, diseases, classification))
            {
                continue;
            }

            instance.SuppressionRemaining = Accumulate(instance.SuppressionRemaining, duration, maxDuration);
            suppressed = true;
        }

        return suppressed;
    }

    #endregion

    #region Queries

    /// <summary>
    /// Проверяет, болеет ли сущность указанной болезнью.
    /// </summary>
    public bool HasDisease(Entity<DiseaseCarrierComponent?> ent, EntityUid disease)
    {
        return _carrierQuery.Resolve(ent, ref ent.Comp, false) && IndexOf(ent.Comp, disease) >= 0;
    }

    /// <summary>
    /// Возвращает состояние болезни у носителя.
    /// </summary>
    public bool TryGetDisease(
        Entity<DiseaseCarrierComponent?> ent,
        EntityUid disease,
        [NotNullWhen(true)] out DiseaseInstance? instance)
    {
        instance = null;
        if (!_carrierQuery.Resolve(ent, ref ent.Comp, false))
            return false;

        var index = IndexOf(ent.Comp, disease);
        if (index < 0)
            return false;

        instance = ent.Comp.Diseases[index];
        return true;
    }

    /// <summary>
    /// Возвращает все болезни сущности. Пусто, если сущность не заражена.
    /// </summary>
    public IReadOnlyList<DiseaseInstance> GetDiseases(Entity<DiseaseCarrierComponent?> ent)
    {
        return _carrierQuery.Resolve(ent, ref ent.Comp, false)
            ? ent.Comp.Diseases
            : Array.Empty<DiseaseInstance>();
    }

    #endregion

    #region Helpers

    private void Cure(Entity<DiseaseCarrierComponent> ent, int index, DiseaseCureReason reason)
    {
        var instance = ent.Comp.Diseases[index];
        ent.Comp.Diseases.RemoveAt(index);
        ResolveSymptoms(ent, instance, fromStage: 0);

        if (reason is DiseaseCureReason.Treatment or DiseaseCureReason.Natural &&
            _registry.TryGetData(instance.Disease, out var proto))
        {
            GrantImmunity(ent, instance.Disease, proto.Immunity);
        }

        _adminLog.Add(LogType.Disease, LogImpact.Low,
            $"{ToPrettyString(ent):target} recovered from {ToPrettyString(instance.Disease)} ({reason})");

        var ev = new DiseaseCuredEvent(instance.Disease, reason);
        RaiseLocalEvent(ent, ref ev);

        // Компонент носителя существует, только пока есть хотя бы одна болезнь.
        if (ent.Comp.Diseases.Count == 0)
            RemComp(ent, ent.Comp);
    }

    private static TimeSpan Accumulate(TimeSpan current, TimeSpan duration, TimeSpan maxDuration)
    {
        var total = current + duration;
        return total > maxDuration ? maxDuration : total;
    }

    private static int IndexOf(DiseaseCarrierComponent carrier, EntityUid disease)
    {
        for (var i = 0; i < carrier.Diseases.Count; i++)
        {
            if (carrier.Diseases[i].Disease == disease)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Подходит ли болезнь под фильтр лекарства.
    /// </summary>
    /// <remarks>
    /// Лекарства перечисляют архетипы, а не конкретные штаммы: сыворотка от гриппа действует
    /// на любой грипп этого раунда, в том числе на выведенный из него штамм.
    /// </remarks>
    private static bool MatchesFilter(
        EntityUid id,
        DiseaseDefinitionComponent data,
        List<ProtoId<DiseasePrototype>>? diseases,
        DiseaseClassification? classification)
    {
        if (diseases is { Count: > 0 })
            return data.Template is { } template && diseases.Contains(template);

        return classification == null || data.Classification == classification;
    }

    #endregion
}

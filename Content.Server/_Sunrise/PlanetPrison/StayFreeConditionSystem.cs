using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.PlanetPrison;

public sealed partial class StayFreeConditionSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;

    private readonly EntProtoId _stayFreeObjective = "PlanetPrisonerStayFreeObjective";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StayFreeConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<CuffableComponent, CuffedStateChangeEvent>(OnCuffedStateChanged);
    }

    private void OnGetProgress(Entity<StayFreeConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(args.MindId, args.Mind);
    }

    /// <summary>
    /// Проверяет, скован ли игрок (в наручниках, смирительной рубашке и т.п.)
    /// Игрок считается скованным, если:
    /// 1. Не может взаимодействовать руками (CanStillInteract == false)
    /// 2. Все руки закованы (CuffedHandCount >= общее количество рук)
    /// </summary>
    private bool IsRestrained(EntityUid entity)
    {
        if (!TryComp<CuffableComponent>(entity, out var cuffed))
            return false;

        if (!cuffed.CanStillInteract)
            return true;

        return TryComp<HandsComponent>(entity, out var hands) && cuffed.CuffedHandCount >= hands.Count;
    }

    private float GetProgress(EntityUid mindId, MindComponent mind)
    {
        if (mind.OwnedEntity == null || _mind.IsCharacterDeadIc(mind))
            return 0f;

        var entity = mind.OwnedEntity.Value;
        var isRestrained = IsRestrained(entity);
        var endReached = IsRoundEndReached();

        // Во время раунда: жив и не закован - 50%, жив, но закован - 10%.
        // В конце раунда: жив и не закован - 100%, жив, но закован - 50%.
        return endReached
            ? isRestrained ? 0.5f : 1f
            : isRestrained ? 0.1f : 0.5f;
    }

    /// <summary>
    /// Проверяет, достигнут ли конец раунда. Цель должна проверяться только в конце раунда или при эвакуации.
    /// Условия окончания раунда:
    /// 1. Прибыл эвакуационный шаттл
    /// 2. Запрошено окончание раунда (например, через админ-команду endround)
    /// 3. Раунд уже не в состоянии InRound (обрабатывает случай, когда раунд уже завершён)
    /// </summary>
    private bool IsRoundEndReached()
    {
        return _emergencyShuttle.EmergencyShuttleArrived
            || _roundEnd.IsRoundEndRequested()
            || _gameTicker.RunLevel != GameRunLevel.InRound;
    }

    private void OnCuffedStateChanged(Entity<CuffableComponent> ent, ref CuffedStateChangeEvent args)
    {
        if (!_mind.TryGetMind(ent.Owner, out var mindId, out var mind))
            return;

        if (!_mind.TryFindObjective((mindId, mind), _stayFreeObjective, out var objectiveUid))
            return;

        if (!TryComp<StayFreeConditionComponent>(objectiveUid.Value, out var conditionComp))
            return;

        // Критически важно использовать единую логику проверки isRestrained:
        // рассинхронизация между иконкой и прогрессом приведёт к багу, когда игрок видит иконку свободного,
        // но прогресс соответствует закованному (или наоборот).
        if (IsRestrained(ent.Owner))
        {
            ApplyRestrainedIcon(objectiveUid.Value, conditionComp);
            return;
        }

        RestoreOriginalIcon(objectiveUid.Value, conditionComp);
    }

    /// <summary>
    /// Применяет иконку закованного состояния к цели.
    /// Сохраняет оригинальную иконку перед заменой, чтобы можно было восстановить её при освобождении.
    /// </summary>
    private void ApplyRestrainedIcon(EntityUid objectiveUid, StayFreeConditionComponent conditionComp)
    {
        // SetIcon перезаписывает исходное значение, поэтому сохраняем оригинал для последующего восстановления
        // при развязывании наручников. Флаг предотвращает повторное сохранение (перезапись может изменить иконку).
        if (!conditionComp.IconOverridden)
        {
            if (TryComp<ObjectiveComponent>(objectiveUid, out var objComp) && objComp.Icon != null)
                conditionComp.OriginalIcon = objComp.Icon;

            conditionComp.IconOverridden = true;
        }

        _objectives.SetIcon(objectiveUid, conditionComp.RestrainedIcon);
    }

    /// <summary>
    /// Восстанавливает оригинальную иконку цели после освобождения игрока.
    /// Использует сохранённую иконку или получает её из прототипа, если сохранение не произошло.
    /// </summary>
    private void RestoreOriginalIcon(EntityUid objectiveUid, StayFreeConditionComponent conditionComp)
    {
        // SetIcon не откатывается автоматически, поэтому явно восстанавливаем сохранённое значение.
        if (!conditionComp.IconOverridden)
            return;

        // Если OriginalIcon был сохранён, используем его. Иначе получаем иконку из прототипа цели
        // (защита от случая, когда иконка отсутствовала на момент сохранения).
        var iconToRestore = conditionComp.OriginalIcon ?? GetIconFromPrototype();

        if (iconToRestore != null)
            _objectives.SetIcon(objectiveUid, iconToRestore);

        conditionComp.IconOverridden = false;
    }

    /// <summary>
    /// Получает иконку цели из прототипа как fallback, если оригинальная иконка не была сохранена.
    /// </summary>
    private SpriteSpecifier? GetIconFromPrototype()
    {
        var objectiveProto = _proto.Index(_stayFreeObjective);
        if (objectiveProto.TryGetComponent<ObjectiveComponent>(out var protoObjComp, _componentFactory))
            return protoObjComp.Icon;

        return null;
    }
}


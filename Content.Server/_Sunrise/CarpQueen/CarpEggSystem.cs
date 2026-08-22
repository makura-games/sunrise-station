using System.Numerics;
using Content.Server.Fluids.EntitySystems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Shared._Sunrise.CarpQueen;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.RatKing;
using Content.Shared.Humanoid;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Maps;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Maths;

namespace Content.Server._Sunrise.CarpQueen;

public sealed partial class CarpEggSystem : CarpQueenAccessSystem
{
    [Dependency] private PuddleSystem _puddles = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private IRobustRandom _rand = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _xformSys = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private CarpQueenSystem _carpQueenSystem = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDestructibleSystem _destructible = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;

    private static readonly ProtoId<ReagentPrototype> WaterReagent = "Water";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CarpEggComponent, DestructionEventArgs>(OnEggDestroyed);
        SubscribeLocalEvent<CarpEggComponent, ComponentShutdown>(OnEggShutdown);
        SubscribeLocalEvent<CarpEggComponent, MapInitEvent>(OnEggMapInit);
        SubscribeLocalEvent<CarpEggComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<CarpEggComponent, EntGotRemovedFromContainerMessage>(OnRemovedFromContainer);
        SubscribeLocalEvent<SolutionChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<CarpQueenServantComponent, ComponentStartup>(OnServantStartup);
        SubscribeLocalEvent<PuddleComponent, MapInitEvent>(OnPuddleMapInit);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CarpEggComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var egg, out var xform))
        {
            // Если яйцо пока не готово, периодически перепроверяем условия готовности.
            if (!egg.Eligible)
            {
                egg.Accum += frameTime;
                egg.WaitElapsed += frameTime;
                if (egg.Accum >= egg.CheckInterval)
                {
                    egg.Accum = 0f;
                    TryHatchCheck(uid, egg);
                }

                // Если яйцо слишком долго ждало без жидкости, уничтожаем его как при разрушении.
                if (egg.WaitElapsed >= egg.MaxWaitWithoutLiquid)
                {
                    _destructible.DestroyEntity(uid);
                    continue;
                }
                continue;
            }

            // Яйцо готово: отсчитываем время до вылупления.
            egg.Accum += frameTime;
            if (egg.Accum >= egg.HatchDelay)
            {
                // Перед вылуплением проверяем, что яйцо все еще на жидкости.
                if (TryComp<MapGridComponent>(xform.GridUid, out var grid))
                {
                    var tile = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
                    if (HasSufficientLiquid(tile, egg.RequiredVolume))
                    {
                        Hatch(uid, egg, xform);
                        continue;
                    }
                }

                // Условия больше не подходят.
                egg.Eligible = false;
                egg.Accum = 0f;
                // Не сбрасываем WaitElapsed, чтобы общее время ожидания продолжало копиться до появления жидкости.
                ResetVisual(uid);
            }
        }
    }

    private void OnServantStartup(EntityUid uid, CarpQueenServantComponent servant, ComponentStartup args)
    {
        // Королева есть: следуем за ней и используем ее текущие приказы.
        if (servant.Queen != null && TryComp(servant.Queen.Value, out CarpQueenComponent? queen))
        {
            _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, new EntityCoordinates(servant.Queen.Value, Vector2.Zero));
            // Конвертируем CarpQueenOrderType в RatKingOrderType для совместимости с HTN.
            var ratKingOrder = SharedCarpQueenSystem.ConvertToRatKingOrder(queen.CurrentOrder);
            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrders, ratKingOrder);
            _npc.SetBlackboard(uid, "FollowCloseRange", 1.0f);
            _npc.SetBlackboard(uid, "FollowRange", 1.5f);
        }
        else
        {
            // Королевы нет: по умолчанию используем Loose, чтобы напрямую созданные слуги были активны.
            // Конвертируем в RatKingOrderType для совместимости с HTN.
            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrders, RatKingOrderType.Loose);
        }

        // Если HTN уже есть, принудительно перестраиваем план.
        if (TryComp<HTNComponent>(uid, out var htn))
        {
            if (htn.Plan != null)
                _htn.ShutdownPlan(htn);
            _htn.Replan(htn);
        }
    }

    private void OnEggMapInit(EntityUid uid, CarpEggComponent egg, MapInitEvent args)
    {
        // Откладываем до назначения королевы, чтобы не создавать несвязанных слуг.
        if (egg.Queen == null)
            return;

        TryHatchCheck(uid, egg);
    }

    private void OnAnchorChanged(EntityUid uid, CarpEggComponent egg, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            TryHatchCheck(uid, egg);
    }

    private void OnRemovedFromContainer(EntityUid uid, CarpEggComponent egg, EntGotRemovedFromContainerMessage args)
    {
        TryHatchCheck(uid, egg);
    }

    private void OnSolutionChanged(ref SolutionChangedEvent args)
    {
        // Если лужа изменилась, перепроверяем яйца на этом тайле.
        // Пропускаем сущность, если она удаляется или не имеет нужных компонентов.
        if (!TryComp<PuddleComponent>(args.Solution.Owner, out var _))
            return;

        // Дополнительная проверка безопасности: сущность должна быть валидной.
        if (TerminatingOrDeleted(args.Solution.Owner))
            return;

        var xform = Transform(args.Solution.Owner);
        if (xform.GridUid == null)
            return;
        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return;
        var tile = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
        foreach (var ent in _lookup.GetEntitiesInTile(tile))
        {
            if (TryComp<CarpEggComponent>(ent, out var egg))
                TryHatchCheck(ent, egg);
        }
    }

    private void OnPuddleMapInit(EntityUid uid, PuddleComponent puddle, MapInitEvent args)
    {
        // Появилась новая лужа: проверяем яйца на этом тайле.
        var xform = Transform(uid);
        if (xform.GridUid == null)
            return;
        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return;
        var tile = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
        foreach (var ent in _lookup.GetEntitiesInTile(tile))
        {
            if (TryComp<CarpEggComponent>(ent, out var egg))
                TryHatchCheck(ent, egg);
        }
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        if (!TryComp<MapGridComponent>(ev.Entity, out var grid))
            return;

        foreach (var change in ev.Changes)
        {
            var tile = _map.GetTileRef(ev.Entity, grid, change.GridIndices);
            foreach (var ent in _lookup.GetEntitiesInTile(tile))
            {
                if (TryComp<CarpEggComponent>(ent, out var egg))
                    TryHatchCheck(ent, egg);
            }
        }
    }



    private void TryHatchCheck(EntityUid uid, CarpEggComponent egg)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        // Вылупляем только яйца вне контейнеров.
        if (xform.GridUid == null)
            return;
        if (_containers.IsEntityInContainer(uid))
            return;

        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return;
        var tile = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);

        if (HasSufficientLiquid(tile, egg.RequiredVolume))
        {
            if (!egg.Eligible)
            {
                egg.Eligible = true;
                egg.Accum = 0f;
                egg.WaitElapsed = 0f;
                // Показываем попап локально королеве, если она есть, иначе всем рядом.
                if (egg.Queen != null && Exists(egg.Queen.Value))
                    _popup.PopupEntity(Loc.GetString("carp-egg-activates"), uid, egg.Queen.Value);
                else
                    _popup.PopupEntity(Loc.GetString("carp-egg-activates"), uid);
            }
            UpdateVisualForTile(uid, tile);
        }
        else
        {
            if (egg.Eligible)
            {
                egg.Eligible = false;
                egg.Accum = 0f;
                ResetVisual(uid);
            }
        }
    }

    private bool HasSufficientLiquid(TileRef tile, float required)
    {
        // Проверка объема лужи.
        if (_puddles.TryGetPuddle(tile, out var puddle))
        {
            var vol = _puddles.CurrentVolume(puddle);
            if (vol >= FixedPoint2.New(required))
                return true;
        }

        // Сущность FloorWaterEntity тоже считается достаточным источником жидкости.
        var gridId = tile.GridUid;
        if (gridId.IsValid())
        {
            // Сначала проверяем закрепленные сущности.
            if (TryComp<MapGridComponent>(gridId, out var grid))
            {
                var enumerator = _map.GetAnchoredEntitiesEnumerator(gridId, grid, tile.GridIndices);
                while (enumerator.MoveNext(out EntityUid? ent))
                {
                    if (!ent.HasValue)
                        continue;

                    // Проверяем по ID прототипа.
                    var meta = MetaData(ent.Value);
                    if (meta.EntityPrototype?.ID == "FloorWaterEntity")
                        return true;
                }
            }

            // Также проверяем все сущности на тайле как запасной вариант.
            var entities = _lookup.GetEntitiesInTile(tile);
            foreach (var ent in entities)
            {
                var meta = MetaData(ent);
                if (meta.EntityPrototype?.ID == "FloorWaterEntity")
                    return true;
            }
        }

        return false;
    }

    private void UpdateVisualForTile(EntityUid uid, TileRef tile)
    {
        Color color;
        // Предпочитаем цвет раствора из лужи, если он доступен.
        if (_puddles.TryGetPuddle(tile, out var puddle) && TryComp(puddle, out PuddleComponent? puddleComp) && puddleComp.Solution != null)
        {
            var sol = puddleComp.Solution.Value.Comp.Solution;
            color = sol.GetColor(_protos);
        }
        else
        {
            // Запасной вариант для FloorWaterEntity: используем цвет реагента Water.
            color = _protos.Index(WaterReagent).SubstanceColor;
        }

        // На сервере красим только свет; оттенок спрайта обрабатывается клиентским visualizer.
        _lights.SetColor(uid, color);
        _appearance.SetData(uid, CarpEggVisuals.OverlayColor, color);
    }

    private void ResetVisual(EntityUid uid)
    {
        // Сбрасываем цвет на белый.
        _lights.SetColor(uid, Color.White);
        _appearance.SetData(uid, CarpEggVisuals.OverlayColor, Color.White);
    }

    private void Hatch(EntityUid uid, CarpEggComponent egg, TransformComponent xform)
    {
        // Получаем данные тайла для цвета жидкости и реагентов.
        Color liquidColor = Color.White;
        Dictionary<string, FixedPoint2> rememberedReagents = new();

        if (TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            var tile = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);

            // Получаем цвет жидкости и реагенты из лужи или FloorWaterEntity.
            if (_puddles.TryGetPuddle(tile, out var puddle) && TryComp(puddle, out PuddleComponent? puddleComp) && puddleComp.Solution != null)
            {
                var sol = puddleComp.Solution.Value.Comp.Solution;
                liquidColor = sol.GetColor(_protos);

                // Запоминаем все реагенты в растворе.
                foreach (var (reagentId, quantity) in sol.Contents)
                {
                    rememberedReagents[reagentId.ToString()] = quantity;
                }
            }
            else
            {
                // Запасной вариант для FloorWaterEntity: используем цвет реагента Water.
                liquidColor = _protos.Index(WaterReagent).SubstanceColor;
                rememberedReagents["Water"] = FixedPoint2.New(30); // Считаем это водой.
            }
        }

        // Выбираем прототип для спавна: чаще радужный карп, реже голографический или dungeon.
        string protoId = "MobCarpServantRainbow"; // По умолчанию радужный карп.

        if (egg.Queen != null && TryComp(egg.Queen.Value, out CarpQueenComponent? queen))
        {
            // Используем шансы спавна из компонента королевы.
            var roll = _rand.Next(100);
            var cumulative = 0;
            var selected = false;

            foreach (var (proto, chance) in queen.SpawnChances)
            {
                cumulative += chance;
                if (roll < cumulative)
                {
                    protoId = proto;
                    selected = true;
                    break;
                }
            }

            // Если шанс не сработал (сумма меньше 100), оставляем радужного карпа.
            if (!selected)
                protoId = "MobCarpServantRainbow";
        }

        var mob = Spawn(protoId, xform.Coordinates);

        // Сохраняем память о жидкости.
        var memory = EnsureComp<CarpServantMemoryComponent>(mob);
        memory.LiquidColor = liquidColor;
        memory.RememberedReagents = rememberedReagents;

        // Проверяем, находится ли королева рядом.
        bool queenNearby = false;
        EntityUid? closestFriend = null;
        float closestDistance = float.MaxValue;

        if (egg.Queen != null && Exists(egg.Queen.Value))
        {
            var queenXform = Transform(egg.Queen.Value);
            var queenCoords = queenXform.Coordinates.ToMap(EntityManager, _xformSys);
            var mobCoords = xform.Coordinates.ToMap(EntityManager, _xformSys);
            var distance = (queenCoords.Position - mobCoords.Position).Length();

            // Считаем королеву рядом, если она в настроенном радиусе.
            if (distance <= egg.QueenCheckRange)
                queenNearby = true;
        }

        // Всегда запоминаем ближайших игроков в настроенном радиусе.
        var nearbyEntities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(xform.Coordinates, egg.FriendSearchRange, nearbyEntities);

        var exception = EnsureComp<FactionExceptionComponent>(mob);

        foreach (var entity in nearbyEntities)
        {
            // Check if it's a humanoid (same as MobTomatoKiller uses whitelist with HumanoidAppearanceComponent)
            // This will match both players and AI with humanoid appearance
            if (HasComp<HumanoidProfileComponent>(entity))
            {
                memory.RememberedFriends.Add(entity);

                // Добавляем в исключения фракции, чтобы их не атаковали без приказа королевы.
                // Используем NpcFactionSystem для корректного добавления в список игнорирования.
                if (!_npcFaction.IsIgnored((mob, exception), entity))
                {
                    _npcFaction.IgnoreEntity((mob, exception), (entity, null));
                }

                // Отслеживаем ближайшего друга для следования.
                var entityXform = Transform(entity);
                var entityCoords = entityXform.Coordinates.ToMap(EntityManager, _xformSys);
                var mobCoords = xform.Coordinates.ToMap(EntityManager, _xformSys);
                var friendDistance = (entityCoords.Position - mobCoords.Position).Length();

                if (friendDistance < closestDistance)
                {
                    closestDistance = friendDistance;
                    closestFriend = entity;
                }
            }
        }

        // Если королева рядом, делаем карпа слугой; иначе оставляем обычным карпом.
        if (queenNearby && egg.Queen != null && Exists(egg.Queen.Value))
        {
            // Делаем карпа слугой королевы.
            if (TryComp(egg.Queen, out CarpQueenComponent? qc))
            {
                var queenUid = egg.Queen.Value;
                memory.RememberedFriends.Add(queenUid);
                if (!_npcFaction.IsIgnored((mob, exception), queenUid))
                {
                    _npcFaction.IgnoreEntity((mob, exception), (queenUid, null));
                }

                var comp = EnsureComp<CarpQueenServantComponent>(mob);
                comp.Queen = egg.Queen;
                Dirty(mob, comp);
                qc.Servants.Add(mob);
                // Убираем яйцо из отслеживания.
                qc.Eggs.Remove(uid);

                // Следуем за королевой и выполняем ее приказы.
                _npc.SetBlackboard(mob, NPCBlackboard.FollowTarget, new EntityCoordinates(egg.Queen.Value, Vector2.Zero));
                _carpQueenSystem.UpdateServantNpc(mob, qc.CurrentOrder);
            }
        }
        else
        {
            // Королевы рядом нет: карп ведет себя как обычный карп, аналогично MobTomatoKiller.
            // Убираем компоненты слуги, если они есть.
            RemComp<CarpQueenServantComponent>(mob);

            // Используем обычный HTN-компаунд карпа вместо RatServantCompound.
            if (TryComp<HTNComponent>(mob, out var htn))
            {
                // Меняем корневую HTN-задачу на обычное поведение карпа.
                htn.RootTask = new HTNCompoundTask { Task = "DragonCarpCompound" };
                _htn.Replan(htn);
            }

            // Назначаем ближайшего друга целью следования, если он есть.
            if (closestFriend != null)
            {
                _npc.SetBlackboard(mob, NPCBlackboard.FollowTarget, new EntityCoordinates(closestFriend.Value, Vector2.Zero));
            }

            // Все равно убираем яйцо из отслеживания, если королева существует.
            if (TryComp(egg.Queen, out CarpQueenComponent? qc))
            {
                qc.Eggs.Remove(uid);
            }
        }

        // Применяем цвет к карпу; дальше его обработает visualizer.
        Dirty(mob, memory);
        QueueDel(uid);
    }

    // Публичная точка входа для других систем, например королевы, чтобы перепроверить условия вылупления.
    public void RequestHatchCheck(EntityUid uid)
    {
        if (!TryComp(uid, out CarpEggComponent? egg))
            return;

        TryHatchCheck(uid, egg);
    }

    private void OnEggDestroyed(EntityUid uid, CarpEggComponent egg, DestructionEventArgs args)
    {
        // При разрушении разливаем 2 единицы случайного реагента.
        var reagents = _protos.EnumeratePrototypes<ReagentPrototype>();
        string chosen = null!;
        var count = 0;
        foreach (var r in reagents)
        {
            count++;
            if (_rand.Prob(1f / count))
                chosen = r.ID;
        }

        if (chosen != null)
        {
            var sol = new Solution(chosen, FixedPoint2.New(2));
            _puddles.TrySpillAt(uid, sol, out _, sound: false);
        }

        // Убираем яйцо из отслеживания королевы.
        if (egg.Queen != null && TryComp(egg.Queen.Value, out CarpQueenComponent? queen))
        {
            queen.Eggs.Remove(uid);
        }
    }

    private void OnEggShutdown(EntityUid uid, CarpEggComponent egg, ComponentShutdown args)
    {
        if (egg.Queen != null && TryComp(egg.Queen.Value, out CarpQueenComponent? queen))
        {
            queen.Eggs.Remove(uid);
        }
    }
}


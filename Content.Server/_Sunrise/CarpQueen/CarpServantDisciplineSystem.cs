using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Sunrise.CarpQueen;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Sunrise.CarpQueen;

/// <summary>
/// Система обрабатывает дисциплину прирученных карпов, выращенных игроком.
/// Если владелец бьет прирученного карпа голыми руками или перчатками, карп прекращает атаковать текущую цель.
/// Карп продолжит атаковать эту цель, если атакующий повредит владельца.
/// </summary>
public sealed partial class CarpServantDisciplineSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CarpServantMemoryComponent, AttackedEvent>(OnCarpAttacked);
        // Не подписываемся на DamageChangedEvent здесь, чтобы не дублировать CarpServantRetaliationSystem.
        // Логика дисциплины, удаляющая цели из запретного списка, обрабатывается отдельно.
    }

    private void OnCarpAttacked(EntityUid uid, CarpServantMemoryComponent memory, AttackedEvent args)
    {
        // Обрабатываем только прирученного карпа, а не слугу королевы.
        if (HasComp<CarpQueenServantComponent>(uid))
            return;

        // Проверяем, является ли атакующий одним из запомненных друзей, то есть владельцем.
        if (!memory.RememberedFriends.Contains(args.User))
            return;

        // Проверяем, была ли атака выполнена голыми руками или предметом в слоте перчаток.
        // Used == User означает отсутствие оружия, то есть голые руки.
        // Либо Used — это предмет, экипированный в слот перчаток пользователя.
        var isBareHands = args.Used == args.User;
        var isGlovesSlotItem = _inventory.TryGetSlotEntity(args.User, "gloves", out var glovesSlotItem) && glovesSlotItem == args.Used;

        if (!isBareHands && !isGlovesSlotItem)
            return;

        // Получаем текущую цель атаки из NPCMeleeCombatComponent или blackboard.
        EntityUid? currentTarget = null;
        NPCMeleeCombatComponent? meleeCombat = null;
        HTNComponent? htn = null;

        // Сначала пробуем NPCMeleeCombatComponent как самый прямой источник.
        if (TryComp<NPCMeleeCombatComponent>(uid, out meleeCombat) && meleeCombat.Target != EntityUid.Invalid)
        {
            currentTarget = meleeCombat.Target;
        }
        // Если не получилось, проверяем распространенные ключи цели в blackboard.
        else if (TryComp<HTNComponent>(uid, out htn))
        {
            // Сначала пробуем CurrentOrderedTarget, который задается королевой/приказами.
            if (htn.Blackboard.TryGetValue<EntityUid>(NPCBlackboard.CurrentOrderedTarget, out var orderedTarget, EntityManager) && orderedTarget != EntityUid.Invalid)
                currentTarget = orderedTarget;
            // Затем пробуем общий ключ "Target".
            else if (htn.Blackboard.TryGetValue<EntityUid>("Target", out var target, EntityManager) && target != EntityUid.Invalid)
                currentTarget = target;
        }

        // Если текущая цель есть, добавляем ее в запрещенные цели.
        if (currentTarget != null && currentTarget != args.User)
        {
            memory.ForbiddenTargets.Add(currentTarget.Value);
            Dirty(uid, memory);

            // Добавляем в исключения фракции, чтобы предотвратить атаку.
            var exception = EnsureComp<FactionExceptionComponent>(uid);
            if (!_npcFaction.IsIgnored((uid, exception), currentTarget.Value))
            {
                _npcFaction.IgnoreEntity((uid, exception), (currentTarget.Value, null));
            }

            // Очищаем цель атаки в blackboard или combat-компоненте.
            if (meleeCombat != null)
            {
                meleeCombat.Target = EntityUid.Invalid;
            }
            else if (htn != null)
            {
                // Очищаем ключи цели в blackboard.
                if (htn.Blackboard.ContainsKey("Target"))
                    _npc.SetBlackboard(uid, "Target", EntityUid.Invalid);
                if (htn.Blackboard.ContainsKey(NPCBlackboard.CurrentOrderedTarget))
                    _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrderedTarget, EntityUid.Invalid);
            }
        }
    }
}

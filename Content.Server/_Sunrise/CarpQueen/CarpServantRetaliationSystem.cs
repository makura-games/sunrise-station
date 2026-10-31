using Content.Shared._Sunrise.CarpQueen;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;

namespace Content.Server._Sunrise.CarpQueen;

/// <summary>
/// Система заставляет прирученных карпов отвечать на атаку сущностей, которые вредят их запомненным друзьям.
/// </summary>
public sealed partial class CarpServantRetaliationSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _npcFaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(EntityUid uid, DamageableComponent component, DamageChangedEvent args)
    {
        // Реагируем только на увеличение урона.
        if (!args.DamageIncreased)
            return;

        // Получаем пострадавшую сущность: uid — это сущность, получившая урон.
        var damagedEntity = uid;

        // Получаем атакующего.
        if (args.Origin is not { } attacker)
            return;

        // Не отвечаем на атаку неодушевленных объектов.
        if (!HasComp<MobStateComponent>(attacker))
            return;

        // Ищем всех карпов, которые помнят пострадавшую сущность как друга.
        var query = EntityQueryEnumerator<CarpServantMemoryComponent>();
        while (query.MoveNext(out var carpUid, out var memory))
        {
            // Пропускаем карпа, если он удаляется.
            if (TerminatingOrDeleted(carpUid))
                continue;

            // Проверяем, входит ли пострадавшая сущность в список запомненных друзей этого карпа.
            if (!memory.RememberedFriends.Contains(damagedEntity))
                continue;

            var attackerIsFriend = memory.RememberedFriends.Contains(attacker);
            var isServant = TryComp<CarpQueenServantComponent>(carpUid, out var servant) && servant.Queen != null;

            if (isServant)
            {
                // Слуги отвечают на атаку только при уроне королеве.
                if (servant!.Queen != damagedEntity)
                    continue;
            }
            else
            {
                // Свободные карпы не отвечают на атаку других запомненных друзей.
                if (attackerIsFriend)
                    continue;
            }

            // Переводим агрессию на атакующего.
            var exception = EnsureComp<FactionExceptionComponent>(carpUid);

            // Также обрабатываем дисциплину: если атакующий был в запрещенных целях, убираем его.
            // Это позволяет карпу снова атаковать после того, как атакующий повредил владельца.
            if (memory.ForbiddenTargets.Remove(attacker))
            {
                Dirty(carpUid, memory);
            }

            // Снимаем игнор и агримся на атакующего, чтобы атака стала возможна после снятия дисциплины.
            _npcFaction.UnignoreEntity((carpUid, exception), attacker);
            _npcFaction.AggroEntity((carpUid, exception), (attacker, null));
        }
    }
}

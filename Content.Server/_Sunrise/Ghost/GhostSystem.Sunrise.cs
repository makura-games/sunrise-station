using System.Diagnostics.CodeAnalysis;
using Content.Shared._Sunrise.Ghost;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.SSDIndicator;
using Content.Shared.Warps;
using Robust.Shared.Prototypes;

// Не менять
namespace Content.Server.Ghost;

public sealed partial class GhostSystem
{
    // Прототип-fallback департамента. Если у сущности нет департамента, он будет отображаться как этот
    private static readonly ProtoId<DepartmentPrototype> UnknownDepartmentPrototype = "Specific";

    /// <summary>
    /// Создает список всех точек телепорта локаций
    /// </summary>
    /// <returns>Созданный список</returns>
    private IEnumerable<GhostWarpPlace> GetLocationWarps()
    {
        var query = AllEntityQuery<WarpPointComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var component, out var meta))
        {
            var warp = new GhostWarpPlace(
                GetNetEntity(uid),
                component.Location ?? meta.EntityName,
                component.Location ?? meta.EntityDescription);

            yield return warp;
        }
    }

    /// <summary>
    /// Создает список всех игроков для отображения в панели телепорта
    /// </summary>
    /// <returns>Созданный список</returns>
    private IEnumerable<GhostWarpPlayer> GetPlayerWarps()
    {
        var query = AllEntityQuery<MindContainerComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var mind, out var meta))
        {
            var isGhost = HasComp<GhostComponent>(uid);
            var isMob = HasComp<MobStateComponent>(uid);

            if (!IsEntityPanelRelevant(uid, isGhost, isMob))
                continue;

            var playerMind = mind.Mind ?? mind.LastMindStored;
            TryGetPlayerJobInfo(playerMind, out var playerJobId, out var playerDepartmentId);

            var hasAnyMind = playerMind != null;

            // Проверка на стандартных мобов, в которых нет игрока
            // Они могут проходить через IsEntityPanelRelevant(), то по сути никогда не являлись игроками
            var isSimpleMob = isMob && !hasAnyMind;
            if (isSimpleMob)
                continue;

            var isDead = _mobState.IsDead(uid);
            var isLeft = TryComp<SSDIndicatorComponent>(uid, out var indicator) && indicator.IsSSD && !isDead && hasAnyMind;

            var warp = new GhostWarpPlayer(
                GetNetEntity(uid),
                meta.EntityName,
                playerJobId,
                playerDepartmentId,
                isGhost,
                isLeft,
                isDead
            );

            yield return warp;
        }
    }

    private bool TryGetPlayerJobInfo(EntityUid? mind,
        [NotNullWhen(true)] out string? job, out string department)
    {
        department = _prototypeManager.Index(UnknownDepartmentPrototype).ID;
        job = null;

        if (!_jobs.MindTryGetJob(mind, out var jobPrototype))
            return false;

        if (!_jobs.TryGetDepartment(jobPrototype.ID, out var departmentPrototype))
            return false;

        job = jobPrototype.ID;
        department = departmentPrototype.ID;

        return true;
    }

    /// <summary>
    /// Создает список всех игроков с ролью антагониста для отображения в панели призрака
    /// </summary>
    /// <returns>Созданный список</returns>
    private IEnumerable<GhostWarpGlobalAntagonist> GetAntagonistWarps()
    {
        var query = AllEntityQuery<GhostPanelAntagonistMarkerComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var component, out var meta))
        {
            if (!component.Enabled)
                continue;

            var warp = new GhostWarpGlobalAntagonist(
                GetNetEntity(uid),
                meta.EntityName,
                component.Name,
                component.Description,
                component.Priority
            );

            yield return warp;
        }
    }

    /// <summary>
    /// Проверяет, является ли живая сущность подходящей для нахождения в панели телепорта призрака
    /// </summary>
    private bool IsEntityPanelRelevant(EntityUid uid, bool isGhost, bool isMob)
    {
        return !HasComp<GhostPanelAntagonistMarkerComponent>(uid) &&
            (isMob || isGhost
                   || HasComp<BorgChassisComponent>(uid)
                   || HasComp<StationAiHeldComponent>(uid));
    }
}

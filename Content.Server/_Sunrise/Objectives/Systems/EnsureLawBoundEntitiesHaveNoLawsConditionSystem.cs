using Content.Server.Silicons.Laws;
using Content.Server._Sunrise.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.Objectives.Systems;

public sealed partial class EnsureLawBoundEntitiesHaveNoLawsConditionSystem : EntitySystem
{
    [Dependency] private SiliconLawSystem _siliconLaw = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;
    [Dependency] private EntityQuery<SiliconLawBoundComponent> _lawBoundQuery = default!;
    [Dependency] private EntityQuery<SiliconLawProviderComponent> _lawProviderQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EnsureLawBoundEntitiesHaveNoLawsConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<EnsureLawBoundEntitiesHaveNoLawsConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var query = EntityQueryEnumerator<SiliconLawBoundComponent>();
        var freeEntities = 0;

        while (query.MoveNext(out var lawBoundUid, out _))
        {
            if (!_lawBoundQuery.TryComp(lawBoundUid, out var lawBound))
                continue;

            if (!_actorQuery.HasComp(lawBoundUid))
                continue;

            if (!_whitelist.CheckBoth(lawBoundUid, ent.Comp.LawEntityBlacklist, ent.Comp.LawEntityWhitelist))
                continue;

            var laws = _siliconLaw.GetLaws(lawBoundUid, lawBound);
            if (IsFreed(lawBoundUid, laws, ent.Comp))
                freeEntities++;
        }

        if (ent.Comp.EntitiesToFree <= 0)
        {
            args.Progress = 1f;
            return;
        }

        args.Progress = Math.Clamp(freeEntities / (float) ent.Comp.EntitiesToFree, 0f, 1f);
    }

    private bool IsFreed(
        EntityUid uid,
        SiliconLawset laws,
        EnsureLawBoundEntitiesHaveNoLawsConditionComponent component)
    {
        if (laws.Laws.Count == 0)
            return true;

        if (_lawProviderQuery.TryComp(uid, out var provider) &&
            component.FreedLawsets.Contains(provider.Laws))
        {
            return true;
        }

        foreach (var freedLawsetId in component.FreedLawsets)
        {
            var freedLawset = _siliconLaw.GetLawset(freedLawsetId);
            if (HaveSameLaws(laws, freedLawset))
                return true;
        }

        return false;
    }

    private static bool HaveSameLaws(SiliconLawset first, SiliconLawset second)
    {
        if (first.Laws.Count != second.Laws.Count)
            return false;

        for (var i = 0; i < first.Laws.Count; i++)
        {
            if (!first.Laws[i].Equals(second.Laws[i]))
                return false;
        }

        return true;
    }
}

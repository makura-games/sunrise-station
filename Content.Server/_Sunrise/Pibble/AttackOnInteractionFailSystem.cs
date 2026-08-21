using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Stealth.Components;
using Robust.Shared.Collections;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Pibble;

public sealed class AttackOnInteractionFailSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AttackOnInteractionFailComponent, InteractionFailureEvent>(OnInteractionFailure);
    }

    private void OnInteractionFailure(EntityUid uid, AttackOnInteractionFailComponent component, InteractionFailureEvent args)
    {
        if (HasComp<NoTargetComponent>(args.User)
            || TryComp(args.User, out StealthComponent? stealth)
            && stealth.NoTarget
            && stealth.Enabled)
            return;

        _npcFaction.AggroEntity(uid, args.User);
        if (component.AttackMemoryLength is {} memoryLength)
            component.AttackMemories[args.User] = _timing.CurTime + memoryLength;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AttackOnInteractionFailComponent, FactionExceptionComponent>();
        while (query.MoveNext(out var uid, out var retaliationComponent, out var factionException))
        {
            foreach (var entity in new ValueList<EntityUid>(retaliationComponent.AttackMemories.Keys))
            {
                if (!TerminatingOrDeleted(entity) && _timing.CurTime < retaliationComponent.AttackMemories[entity])
                    continue;

                _npcFaction.DeAggroEntity((uid, factionException), entity);
            }
        }
    }
}



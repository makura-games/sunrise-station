using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Content.Shared._Sunrise.Tutorial.EntitySystems.SoftLock;
using Content.Shared.EntityEffects;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems;

/// <summary>
/// Shared tutorial flow controller for step progression, condition checks, bubbles, and tracked targets.
/// </summary>
public abstract partial class SharedTutorialSystem : EntitySystem
{
    [Dependency] private SharedTutorialConditionsSystem _tutorial = default!;
    [Dependency] private TutorialSoftLockSystem _softLock = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EntityLookupSystem _lookupSystem = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedEntityEffectsSystem _entityEffects = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialPlayerComponent, EntityTerminatingEvent>(OnTutorialPlayerTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.ApplyingState)
            return;

        var query = EntityQueryEnumerator<TutorialPlayerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (!comp.TutorialInitialized)
                continue;

            if (comp.EndTime != null && _timing.CurTime > comp.EndTime)
            {
                EndTutorial((uid, comp));
                continue;
            }

            CheckCondition((uid, comp));
        }
    }

    /// <summary>
    /// Performs first-time setup for a tutorial session: starts the timer, initialises
    /// the first step, and fires all related side-effects.
    /// Must be called after <see cref="TutorialPlayerComponent.SequenceId"/> and
    /// <see cref="TutorialPlayerComponent.Grid"/> are fully configured.
    /// </summary>
    public void InitializeTutorial(Entity<TutorialPlayerComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || ent.Comp.TutorialInitialized)
            return;

        ent.Comp.ActiveStepOverride = null;

        if (!TryGetCurrentStep(ent, out var step))
            return;

        ent.Comp.TutorialInitialized = true;
        ent.Comp.EndTime = _timing.CurTime + _proto.Index(ent.Comp.SequenceId).Duration;
        UpdateTimeCounter(ent, ent.Comp.EndTime);
        OnStepChanged(ent, step);
    }

    private void OnTutorialPlayerTerminating(Entity<TutorialPlayerComponent> ent, ref EntityTerminatingEvent args)
    {
        ClearTracking(ent);
    }

    /// <summary>
    /// Updates side-specific time counter state for a tutorial session.
    /// </summary>
    protected virtual void UpdateTimeCounter(Entity<TutorialPlayerComponent> ent, TimeSpan? endTime)
    {
    }
}

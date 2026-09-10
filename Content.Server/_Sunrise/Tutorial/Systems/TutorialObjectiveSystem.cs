using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Events;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Tutorial.Systems;

/// <summary>
/// Owns Objective System instances for the currently active tutorial step.
/// </summary>
public sealed class TutorialObjectiveSystem : EntitySystem
{
    [Dependency] private readonly ObjectiveSystem _objectives = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private const string CompletionSource = "TutorialCompletion";
    private const string PreconditionsSource = "TutorialPreconditions";
    private const string FailureSourcePrefix = "TutorialFailure:";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialStepActivatedEvent>(OnStepActivated);
        SubscribeLocalEvent<TutorialTrackingEndedEvent>(OnTrackingEnded);
        SubscribeLocalEvent<ObjectiveStateChangedEvent>(OnObjectiveStateChanged);
        SubscribeLocalEvent<ObjectiveStoppedEvent>(OnObjectiveStopped);
    }

    private void OnStepActivated(ref TutorialStepActivatedEvent args)
    {
        ClearStepObjectives(args.Player);
        if (!_prototype.TryIndex(args.Step, out var step))
            return;

        var runtime = EnsureComp<TutorialStepObjectivesComponent>(args.Player);
        runtime.PreconditionsSatisfied = step.Preconditions == null;

        if (!TryStart(
                args.Player,
                step.Completion,
                ObjectiveRunMode.OneShot,
                CompletionSource,
                step.ObserveRange,
                out runtime.Completion,
                out runtime.CompletionSatisfied))
        {
            Log.Error("Failed to start completion objective for tutorial step {Step} and player {Player}",
                step.ID,
                ToPrettyString(args.Player));
        }

        if (step.Preconditions != null &&
            TryStart(
                args.Player,
                step.Preconditions,
                ObjectiveRunMode.Monitor,
                PreconditionsSource,
                step.ObserveRange,
                out var preconditions,
                out var preconditionsSatisfied))
        {
            runtime.Preconditions = preconditions;
            runtime.PreconditionsSatisfied = preconditionsSatisfied;
        }

        for (var i = 0; i < step.Failures.Count; i++)
        {
            var failure = step.Failures[i];
            if (!TryStart(
                    args.Player,
                    failure.When,
                    ObjectiveRunMode.Monitor,
                    string.Concat(FailureSourcePrefix, i),
                    step.ObserveRange,
                    out var objective,
                    out var satisfied))
            {
                Log.Error("Failed to start failure objective {Failure} for tutorial step {Step} and player {Player}",
                    i,
                    step.ID,
                    ToPrettyString(args.Player));
                runtime.Failures.Add(EntityUid.Invalid);
                runtime.FailuresSatisfied.Add(false);
                continue;
            }

            runtime.Failures.Add(objective);
            runtime.FailuresSatisfied.Add(satisfied);
        }
    }

    private void OnTrackingEnded(ref TutorialTrackingEndedEvent args)
    {
        ClearStepObjectives(args.Player);
    }

    private void OnObjectiveStateChanged(ref ObjectiveStateChangedEvent args)
    {
        if (!TryComp(args.Owner, out TutorialStepObjectivesComponent? runtime))
            return;

        if (runtime.Completion == args.Objective)
        {
            runtime.CompletionSatisfied = args.Satisfied;
            return;
        }

        if (runtime.Preconditions == args.Objective)
        {
            runtime.PreconditionsSatisfied = args.Satisfied;
            return;
        }

        var failure = runtime.Failures.IndexOf(args.Objective);
        if (failure >= 0 && failure < runtime.FailuresSatisfied.Count)
            runtime.FailuresSatisfied[failure] = args.Satisfied;
    }

    private void OnObjectiveStopped(ref ObjectiveStoppedEvent args)
    {
        if (!TryComp(args.Owner, out TutorialStepObjectivesComponent? runtime))
            return;

        if (runtime.Completion == args.Objective)
            runtime.CompletionSatisfied = false;

        if (runtime.Preconditions == args.Objective)
            runtime.PreconditionsSatisfied = false;

        var failure = runtime.Failures.IndexOf(args.Objective);
        if (failure >= 0 && failure < runtime.FailuresSatisfied.Count)
            runtime.FailuresSatisfied[failure] = false;
    }

    private bool TryStart(
        EntityUid player,
        ObjectiveDefinition definition,
        ObjectiveRunMode mode,
        string source,
        float range,
        out EntityUid objective,
        out bool satisfied)
    {
        var options = new ObjectiveStartOptions
        {
            Mode = mode,
            CompletionRetention = ObjectiveCompletionRetention.Retain,
            ObservationRange = range,
            SourceIdentifier = source,
        };

        satisfied = false;
        if (!_objectives.TryStartObjective(player, definition, options, out objective) ||
            !_objectives.TryGetObjectiveStatus(objective, out var status))
        {
            return false;
        }

        satisfied = status.Satisfied;
        return true;
    }

    private void ClearStepObjectives(EntityUid player)
    {
        if (!TryComp(player, out TutorialStepObjectivesComponent? runtime))
            return;

        var objectives = new List<EntityUid>(runtime.Failures.Count + 2)
        {
            runtime.Completion,
        };

        if (runtime.Preconditions is { } preconditions)
            objectives.Add(preconditions);

        objectives.AddRange(runtime.Failures);
        RemComp<TutorialStepObjectivesComponent>(player);

        for (var i = 0; i < objectives.Count; i++)
        {
            if (objectives[i].Valid && Exists(objectives[i]))
                _objectives.TryStopObjective(objectives[i]);
        }
    }
}

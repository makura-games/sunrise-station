using Content.Shared.Objectives.Components;

namespace Content.Server._Sunrise.PlanetPrison;

public sealed partial class EscapePrisonConditionSystem : EntitySystem
{
    [Dependency] private PlanetPrisonSystem _planetPrisonSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EscapePrisonConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, EscapePrisonConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = _planetPrisonSystem.PrisonerEscaped(args.MindId) ? 1f : 0f;
    }
}

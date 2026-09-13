using Content.Server.Examine;

namespace Content.Server._Sunrise.Chat;

public sealed partial class EmoteVisibilityCheckSystem : EntitySystem
{
    [Dependency] private ExamineSystem _examineSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmoteVisibilityCheckEvent>(HandleVisibilityCheck);
    }

    private void HandleVisibilityCheck(ref EmoteVisibilityCheckEvent ev)
    {
        if (ev.Target is null)
        {
            ev.Visible = false;
            return;
        }

        if (!_examineSystem.InRangeUnOccluded(ev.Source, ev.Target.Value, ev.Range))
        {
            ev.Visible = false;
            return;
        }
    }
}

using Content.Server.Radio;
using Content.Shared._Sunrise.Tutorial.Components;

namespace Content.Server._Sunrise.Tutorial;

public sealed partial class TutorialRadioIsolationSystem : EntitySystem
{
    [Dependency] private EntityQuery<TutorialPlayerComponent> _tutorialPlayerQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _transformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);

    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var sender = GetRadioActor(args.RadioSource);
        var receiver = GetRadioActor(args.RadioReceiver);

        var senderInTutorial = _tutorialPlayerQuery.HasComp(sender);
        var receiverInTutorial = _tutorialPlayerQuery.HasComp(receiver);

        if (!senderInTutorial && !receiverInTutorial)
            return;

        if (sender == receiver)
            return;

        args.Cancelled = true;
    }

    private EntityUid GetRadioActor(EntityUid radio)
    {
        if (_tutorialPlayerQuery.HasComp(radio))
            return radio;

        if (!_transformQuery.TryComp(radio, out var transform))
            return radio;

        var parent = transform.ParentUid;
        return parent.IsValid() ? parent : radio;
    }
}

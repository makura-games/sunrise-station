using System;
using System.Collections.Generic;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Tutorial.Events;

[Serializable, NetSerializable]
public sealed class TutorialQuitRequestEvent : EntityEventArgs
{
}

[NetSerializable, Serializable]
public sealed class TutorialStepChangedEvent() : EntityEventArgs
{
}

[NetSerializable, Serializable]
public sealed class TutorialEndedEvent() : EntityEventArgs
{
}

[NetSerializable, Serializable]
public sealed class TutorialStepsCompletedEvent() : EntityEventArgs
{
}

[NetSerializable, Serializable]
public sealed class TutorialStartRequestEvent(ProtoId<TutorialSequencePrototype> sequenceId) : EntityEventArgs
{
    public ProtoId<TutorialSequencePrototype> SequenceId = sequenceId;
}

[NetSerializable, Serializable]
public sealed class TutorialStartDeniedEvent(string reason) : EntityEventArgs
{
    public string Reason = reason;
}

[NetSerializable, Serializable]
public sealed class TutorialWindowDataRequestEvent() : EntityEventArgs
{
}

[NetSerializable, Serializable]
public sealed class TutorialWindowDataResponseEvent(List<string> completedTutorials) : EntityEventArgs
{
    public List<string> CompletedTutorials = completedTutorials;
}

[NetSerializable, Serializable]
public sealed class TutorialUiButtonPressedEvent(string button) : EntityEventArgs
{
    public string Button = button;
}

[NetSerializable, Serializable]
public sealed class TutorialUiControlVisibleEvent(string control) : EntityEventArgs
{
    public string Control = control;
}

using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.BloodCult.UI;

[Serializable, NetSerializable]
public enum SelectConstructUi
{
    Key
}

[Serializable, NetSerializable]
public sealed class ConstructFormSelectedEvent : BoundUserInterfaceMessage
{
    public string SelectedForm;

    public ConstructFormSelectedEvent(string form)
    {
        SelectedForm = form;
    }
}

using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.BloodCult.UI;

[Serializable, NetSerializable]
public enum ListViewSelectorUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class ListViewItemSelectedMessage : BoundUserInterfaceMessage
{
    public ListViewItemSelectedMessage(string selectedItem)
    {
        SelectedItem = selectedItem;
    }

    public string SelectedItem { get; private set; }
}

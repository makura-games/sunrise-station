using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.BloodCult.Items;

[NetSerializable, Serializable]
public enum CultTeleporterUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class TorchWindowItemSelectedMessage : BoundUserInterfaceMessage
{
    public TorchWindowItemSelectedMessage(string entUid, string selectedItem)
    {
        SelectedItem = selectedItem;
        EntUid = entUid;
    }

    public string SelectedItem { get; private set; }
    public string EntUid { get; private set; }
}

[Serializable, NetSerializable]
public sealed class TorchWindowBUIState : BoundUserInterfaceState
{
    public TorchWindowBUIState(Dictionary<string, string> items)
    {
        Items = items;
    }

    public Dictionary<string, string> Items { get; set; }
}

[Serializable, NetSerializable]
public enum VoidTorchVisuals : byte
{
    Activated
}

using Content.Shared.Store;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.BloodCult.UI;

[Serializable, NetSerializable]
public enum NameSelectorUIKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class NameSelectorBuiState : BoundUserInterfaceState
{
    public NameSelectorBuiState(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
}

[Serializable, NetSerializable]
public sealed class NameSelectorMessage : BoundUserInterfaceMessage
{
    public NameSelectorMessage(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
}

[NetSerializable, Serializable]
public enum RuneTeleporterUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class TeleportRunesListWindowItemSelectedMessage : BoundUserInterfaceMessage
{
    public TeleportRunesListWindowItemSelectedMessage(int selectedItem, int index)
    {
        SelectedItem = selectedItem;
        Index = index;
    }

    public int SelectedItem { get; private set; }
    public int Index { get; private set; }
}

[Serializable, NetSerializable]
public sealed class TeleportRuneChangeNameMessage : BoundUserInterfaceMessage
{
    public TeleportRuneChangeNameMessage()
    {
    }
}

[Serializable, NetSerializable]
public sealed class TeleportRunesListWindowBUIState(List<int> items, List<string> labels, List<string> distance) : BoundUserInterfaceState
{
    public List<int> Items = items;
    public List<string> Label = labels;
    public List<string> Distance = distance;
}

[NetSerializable, Serializable]
public enum SummonCultistUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class SummonCultistListWindowItemSelectedMessage : BoundUserInterfaceMessage
{
    public SummonCultistListWindowItemSelectedMessage(int selectedItem, int index)
    {
        SelectedItem = selectedItem;
        Index = index;
    }

    public int SelectedItem { get; private set; }
    public int Index { get; private set; }
}

[Serializable, NetSerializable]
public sealed class SummonCultistListWindowBUIState(List<int> items, List<string> labels, List<string> mobState, List<string> distance) : BoundUserInterfaceState
{
    public List<int> Items = items;
    public List<string> Label = labels;
    public List<string> MobStates = mobState;
    public List<string> Distances = distance;
}

[Serializable, NetSerializable]
public enum SinguloCallUIKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SinguloCallBuiState : BoundUserInterfaceState
{
    public SinguloCallBuiState(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
}

[Serializable, NetSerializable]
public sealed class SinguloCallMessage : BoundUserInterfaceMessage
{
    public SinguloCallMessage(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
}

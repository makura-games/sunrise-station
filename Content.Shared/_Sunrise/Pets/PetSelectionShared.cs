// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt

using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Pets;

public sealed class PetSelectionInfo
{
    public string Id { get; }
    public bool IsAvailable { get; }

    public PetSelectionInfo(string id, bool isAvailable)
    {
        Id = id;
        IsAvailable = isAvailable;
    }
}

[Serializable, NetSerializable]
public sealed class PetSelectionPrototypeSelectedEvent(string selectedPetSelection) : EntityEventArgs
{
    public string SelectedPetSelection = selectedPetSelection;
}

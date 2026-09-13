// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt

using Content.Shared._Sunrise.Pets;

namespace Content.Client._Sunrise.Pets;

public sealed partial class PetSelectionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
    }

    public void PetSelectionSelected(string selectedPet)
    {
        var message = new PetSelectionPrototypeSelectedEvent(selectedPet);
        RaiseNetworkEvent(message);
    }
}

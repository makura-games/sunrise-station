using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Shared.Hands.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.UserInterface.Systems.Hotbar.Widgets;

public sealed partial class HotbarGui
{
    public void ClearHandButtons()
    {
        HandContainer.ClearButtons();
        FunctionalHandContainer.ClearButtons();
    }

    public void SetPlayerHandsComponent(Entity<HandsComponent> hands)
    {
        HandContainer.PlayerHandsComponent = hands.Comp;
        FunctionalHandContainer.PlayerHandsComponent = hands.Comp;
    }

    public bool TryGetHandButton(string handName, out HandButton? handButton)
    {
        if (HandContainer.TryGetButton(handName, out handButton))
            return true;

        return FunctionalHandContainer.TryGetButton(handName, out handButton);
    }

    public HandButton? GetHandButton(string handName)
    {
        return TryGetHandButton(handName, out var handButton) ? handButton : null;
    }

    public void AddHandButton(HandButton handButton)
    {
        GetHandContainer(handButton.HandLocation).TryAddButton(handButton);
    }

    public bool TryRemoveHandButton(string handName, out HandButton? handButton)
    {
        if (HandContainer.TryRemoveButton(handName, out handButton))
            return true;

        return FunctionalHandContainer.TryRemoveButton(handName, out handButton);
    }

    public IEnumerable<HandButton> GetHandButtons()
    {
        foreach (var handButton in HandContainer.GetButtons())
        {
            yield return handButton;
        }

        foreach (var handButton in FunctionalHandContainer.GetButtons())
        {
            yield return handButton;
        }
    }

    private HandsContainer GetHandContainer(HandLocation location)
    {
        return location == HandLocation.Functional
            ? FunctionalHandContainer
            : HandContainer;
    }
}

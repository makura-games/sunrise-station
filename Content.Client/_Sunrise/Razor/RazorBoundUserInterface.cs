using Content.Shared.Humanoid.Markings;
using Content.Shared._Sunrise.Razor;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Razor;

public sealed class RazorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private RazorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<RazorWindow>();

        _window.OnHairSelected += tuple => SelectHair(RazorCategory.Hair, tuple.id, tuple.slot);
        _window.OnHairSlotAdded += delegate () { AddSlot(RazorCategory.Hair); };
        _window.OnHairSlotRemoved += args => RemoveSlot(RazorCategory.Hair, args);

        _window.OnFacialHairSelected += tuple => SelectHair(RazorCategory.FacialHair, tuple.id, tuple.slot);
        _window.OnFacialHairSlotAdded += delegate () { AddSlot(RazorCategory.FacialHair); };
        _window.OnFacialHairSlotRemoved += args => RemoveSlot(RazorCategory.FacialHair, args);
    }

    private void SelectHair(RazorCategory category, string marking, int slot)
    {
        SendMessage(new RazorSelectMessage(category, marking, slot));
    }

    private void RemoveSlot(RazorCategory category, int slot)
    {
        SendMessage(new RazorRemoveSlotMessage(category, slot));
    }

    private void AddSlot(RazorCategory category)
    {
        SendMessage(new RazorAddSlotMessage(category));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not RazorUiState data || _window == null)
        {
            return;
        }

        _window.UpdateState(data);
    }
}

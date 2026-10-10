using Content.Client._Sunrise.UserInterface.Radial;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Items;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.BloodCult.UI.SpellSelector;

public sealed class SpellSelectorBUI : BoundUserInterface
{
    private RadialContainer? _menu;

    private bool _selected;

    public SpellSelectorBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _menu = new RadialContainer();
        _menu.Closed += () =>
        {
            if (_selected)
                return;

            Close();
        };

        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var sprite = EntMan.System<SpriteSystem>();

        foreach (var action in BloodCultistComponent.CultistActions)
        {
            if (!protoMan.TryIndex(action, out var proto))
                continue;

            var texture = sprite.GetPrototypeIcon(proto).Default;
            var button = _menu.AddButton(proto.Name, texture);

            button.Controller.OnPressed += _ =>
            {
                _selected = true;
                SendMessage(new CultSpellProviderSelectedBuiMessage(action));
                _menu.Close();
                Close();
            };
        }

        _menu.OpenAttached(Owner);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Close();
    }
}

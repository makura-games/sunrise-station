using Content.Client._Sunrise.UserInterface.Radial;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.UI;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client._Sunrise.BloodCult.UI.ConstructSelector;

public sealed partial class ConstructSelectorBui : BoundUserInterface
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;

    private RadialContainer? _menu;
    private bool _selected;
    private SpriteSystem _spriteSystem = default!;

    public ConstructSelectorBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

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

        _spriteSystem = _entityManager.EntitySysManager.GetEntitySystem<SpriteSystem>();
        var shellComponent = _entityManager.GetComponent<ConstructShellComponent>(Owner);

        _menu.Closed += () =>
        {
            if (_selected)
                return;

            SendMessage(new ConstructFormSelectedEvent(_random.Pick(shellComponent.ConstructForms)));
        };

        foreach (var form in shellComponent.ConstructForms)
        {
            var formPrototype = _prototypeManager.Index<EntityPrototype>(form);
            var button = _menu.AddButton(formPrototype.Name, _spriteSystem.GetPrototypeIcon(formPrototype).Default);

            button.Controller.OnPressed += _ =>
            {
                _selected = true;
                SendMessage(new ConstructFormSelectedEvent(form));
                _menu.Close();
                Close();
            };
        }

        _menu.OpenAttached(Owner);
    }
}

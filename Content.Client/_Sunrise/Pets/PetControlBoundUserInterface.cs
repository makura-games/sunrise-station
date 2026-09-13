using Content.Client._Sunrise.UserInterface.Radial;
using Content.Shared._Sunrise.Pets;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Pets;

[UsedImplicitly]
public sealed partial class PetControlBoundUserInterface : BoundUserInterface
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private RadialContainer? _menu;

    private bool _selected;

    private readonly PettingSystem _clientPetSystem;
    private readonly SpriteSystem _spriteSystem;

    public PetControlBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);

        _clientPetSystem = _entityManager.System<PettingSystem>();
        _spriteSystem = _entityManager.System<SpriteSystem>();
    }

    protected override void Open()
    {
        base.Open();

        var pet = _entityManager.GetComponent<PettableOnInteractComponent>(Owner);
        var ourMaster = _playerManager.LocalSession?.AttachedEntity;

        if (ourMaster != pet.Master)
            return;

        _menu = new RadialContainer();

        _menu.Closed += () =>
        {
            if (_selected)
                return;

            Close();
        };

        foreach (var controlId in pet.AvailableControls)
        {
            var control = _prototypeManager.Index(controlId);
            var button = _menu.AddButton(control.Name, _spriteSystem?.Frame0(control.Sprite));

            button.Controller.OnPressed += _ =>
            {
                _selected = true;
                SendPetMessage(control.Event);
                _menu.Close();
                Close();
            };
        }

        _menu.OpenAttached(Owner);
    }

    public void SendPetMessage(PetBaseEvent ev)
    {
        // May god forgive us.
        // Чтобы реализовать поддержку ивентов вместо сообщений буи мне пришлось использовать систему-пустышку.

        _clientPetSystem.RaiseFuckingEvent(Owner, ev);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Close();
    }
}

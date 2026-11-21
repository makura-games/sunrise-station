using Content.Client._Sunrise.UserInterface.Radial;
using Content.Shared._Sunrise.BloodCult.Items;
using Content.Shared._Sunrise.BloodCult.UI;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.BloodCult.UI.ListViewSelector;

public sealed partial class ListViewSelectorBUI : BoundUserInterface
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private RadialContainer? _menu;
    private bool _selected;

    public ListViewSelectorBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
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
        var sprite = _entityManager.System<SpriteSystem>();

        if (!_entityManager.TryGetComponent<RuneDrawerProviderComponent>(Owner, out var component))
            return;

        foreach (var item in component.RunePrototypes)
        {
            if (!_prototypeManager.TryIndex(item, out var runeProto))
                continue;

            var texture = sprite.GetPrototypeIcon(runeProto).Default;
            var button = _menu.AddButton(Loc.GetString($"ent-{item}"), texture);
            button.BackgroundTexture.Modulate = Color.FromHex("#F80000");

            button.Controller.OnPressed += _ =>
            {
                _selected = true;
                var msg = new ListViewItemSelectedMessage(item);
                SendMessage(msg);
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

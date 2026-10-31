using System.Linq;
using Content.Client._Sunrise.UserInterface.Radial;
using Content.Shared._Sunrise.BloodCult.UI;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.BloodCult.UI.CultistFactory;

public sealed partial class CultistFactoryBUI : BoundUserInterface
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private RadialContainer? _menu;

    private bool _selected;

    private bool _updated;

    public CultistFactoryBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    private void ResetUI()
    {
        _menu?.Close();
        _menu = null;
        _updated = false;
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

        if (State != null)
            UpdateState(State);
    }

    private void PopulateRadial(Dictionary<string, List<EntProtoId>> ids)
    {
        var spriteSys = _entityManager.EntitySysManager.GetEntitySystem<SpriteSystem>();

        foreach (var (name, items) in ids)
        {
            if (!_prototypeManager.TryIndex<EntityPrototype>(items.First(), out var prototype))
                continue;

            if (_menu == null)
                continue;

            var button = _menu.AddButton(Loc.GetString(name), spriteSys.Frame0(prototype));
            button.Controller.OnPressed += _ =>
            {
                Select(items);
            };
        }
    }

    private void Select(List<EntProtoId> id)
    {
        _selected = true;
        SendMessage(new CultistFactoryItemSelectedMessage(id));
        ResetUI();
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        ResetUI();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_updated)
            return;

        if (state is CultistFactoryBUIState newState)
        {
            PopulateRadial(newState.Ids);
        }

        if (_menu == null)
            return;

        _menu.OpenAttached(Owner);
        _updated = true;
    }
}

using Content.Client._Sunrise.UserInterface.Radial;
using Content.Shared._Sunrise.BloodCult.Items;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.BloodCult.UI.BloodSpellSelector;

public sealed class BloodSpellSelectorBUI : BoundUserInterface
{
    private RadialContainer? _menu;
    private readonly string _bloodOrbProto = "CultBloodOrb";
    private readonly string _bloodSpearProto = "BloodSpear";
    private readonly string _bloodBoltBarrageProto = "BloodBoltBarrage";

    private bool _selected;

    public BloodSpellSelectorBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
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
        var entityMan = IoCManager.Resolve<IEntityManager>();
        var sprite = entityMan.System<SpriteSystem>();

        if (protoMan.TryIndex(_bloodOrbProto, out var bloodOrb))
        {
            var texture = sprite.GetPrototypeIcon(bloodOrb);
            var button = _menu.AddButton($"{bloodOrb.Name} (50)", texture.Default);

            button.Controller.OnPressed += _ =>
            {
                _selected = true;
                SendMessage(new CultBloodSpellCreateOrbBuiMessage());
                _menu.Close();
                Close();
            };
        }

        if (protoMan.TryIndex(_bloodSpearProto, out var bloodSpear))
        {
            var texture = sprite.GetPrototypeIcon(bloodSpear);
            var button = _menu.AddButton($"{bloodSpear.Name} (100)", texture.Default);

            button.Controller.OnPressed += _ =>
            {
                _selected = true;
                SendMessage(new CultBloodSpellCreateBloodSpearBuiMessage());
                _menu.Close();
                Close();
            };
        }

        if (protoMan.TryIndex(_bloodBoltBarrageProto, out var bloodBoltBarrage))
        {
            var texture = sprite.GetPrototypeIcon(bloodBoltBarrage);
            var button = _menu.AddButton($"{bloodBoltBarrage.Name} (200)", texture.Default);

            button.Controller.OnPressed += _ =>
            {
                _selected = true;
                SendMessage(new CultBloodSpellCreateBloodBoltBarrageBuiMessage());
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

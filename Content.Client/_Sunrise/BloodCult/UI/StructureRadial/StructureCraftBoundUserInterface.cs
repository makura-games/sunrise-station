using Content.Client._Sunrise.UserInterface.Radial;
using Content.Client.Construction;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Structures;
using Content.Shared.Construction.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.BloodCult.UI.StructureRadial;

public sealed partial class StructureCraftBoundUserInterface : BoundUserInterface
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IEntitySystemManager _systemManager = default!;

    private RadialContainer? _menu;
    private bool _selected;

    public StructureCraftBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
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
        var sprite = _entMan.System<SpriteSystem>();

        if (_player.LocalEntity == null)
            return;

        if (!_entMan.TryGetComponent<BloodCultistComponent>(_player.LocalEntity.Value, out var cultist) ||
            cultist.CultType == null)
            return;

        foreach (var prototype in _prototypeManager.EnumeratePrototypes<CultStructurePrototype>())
        {
            if (prototype.CultType != cultist.CultType)
                continue;

            var texture = sprite.Frame0(prototype.Icon);
            var radialButton = _menu.AddButton(Loc.GetString(prototype.StructureName), texture);
            radialButton.Controller.OnPressed += _ =>
            {
                _selected = true;
                CreateBlueprint(prototype.StructureId);
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

    private void CreateBlueprint(string id)
    {
        var newObj = new PlacementInformation
        {
            Range = 2,
            IsTile = false,
            EntityType = id,
            PlacementOption = "SnapgridCenter"
        };

        _prototypeManager.TryIndex<ConstructionPrototype>(id, out var construct);

        if (construct == null)
            return;

        var player = _player.LocalSession?.AttachedEntity;

        if (player == null)
            return;

        // Хуйня которая не работает
        // if (construct.ID == "CultPylon" && CheckForStructure(player, id))
        // {
        //     var popup = _entMan.System<SharedPopupSystem>();
        //     popup.PopupClient(Loc.GetString("cult-structure-craft-another-structure-nearby"), player.Value, player.Value);
        //     return;
        // }

        var constructSystem = _systemManager.GetEntitySystem<ConstructionSystem>();
        var hijack = new ConstructionPlacementHijack(constructSystem, construct);

        _placement.BeginPlacing(newObj, hijack);
    }
}

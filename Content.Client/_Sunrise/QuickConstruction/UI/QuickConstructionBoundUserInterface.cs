using Content.Client.Construction;
using Content.Client.UserInterface.Controls;
using Content.Shared._Sunrise.QuickConstruction.Components;
using Content.Shared._Sunrise.QuickConstruction.Prototypes;
using Content.Shared.Construction.Prototypes;
using JetBrains.Annotations;
using Robust.Client.Placement;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Enums;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.QuickConstruction.UI;

[UsedImplicitly]
public sealed partial class QuickConstructionBoundUserInterface : BoundUserInterface
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private ILogManager _log = default!;

    private SimpleRadialMenu? _menu;
    private ConstructionSystem? _constructionSystem;
    private readonly ISawmill _sawmill;

    public QuickConstructionBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _log.GetSawmill("quick-construction.bui");
    }

    protected override void Open()
    {
        base.Open();
        _constructionSystem ??= EntMan.System<ConstructionSystem>();

        if (!EntMan.TryGetComponent<QuickConstructableComponent>(Owner, out var quickConstructable) ||
            !_prototype.TryIndex(quickConstructable.Category, out var rootCategory))
            return;

        var models = ConvertToButtons(
            rootCategory.ConstructionEntries,
            rootCategory.CategoryEntries,
            [quickConstructable.Category]);

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(models);
        _menu.OpenOverMouseScreenPosition();
    }

    private IReadOnlyCollection<RadialMenuOptionBase> ConvertToButtons(
        List<ProtoId<ConstructionPrototype>> constructionEntries,
        List<ProtoId<QuickConstructionCategoryPrototype>> categoryEntries,
        HashSet<ProtoId<QuickConstructionCategoryPrototype>> categoryStack)
    {
        var constructionSystem = _constructionSystem;
        if (constructionSystem == null)
            return [];

        ValueList<RadialMenuActionOptionBase> constructionButtons = [];
        ValueList<(QuickConstructionCategoryPrototype Prototype, IReadOnlyCollection<RadialMenuOptionBase> Buttons)> categoryButtons = [];

        foreach (var constructionEntry in constructionEntries)
        {
            if (!_prototype.TryIndex(constructionEntry, out var constructionPrototype))
            {
                _sawmill.Warning($"Skipping unknown construction prototype '{constructionEntry}' in quick construction menu for entity {Owner}.");
                continue;
            }

            if (!constructionSystem.TryGetRecipePrototype(constructionEntry, out var recipePrototypeId))
            {
                _sawmill.Warning($"Skipping construction prototype '{constructionEntry}' because no construction recipe mapping was found for entity {Owner}.");
                continue;
            }

            if (!_prototype.TryIndex(recipePrototypeId, out var recipePrototype))
            {
                _sawmill.Warning($"Skipping construction prototype '{constructionEntry}' because recipe prototype '{recipePrototypeId}' could not be resolved for entity {Owner}.");
                continue;
            }

            var topLevelActionOption = new RadialMenuActionOption<ConstructionPrototype>(HandlePlacement, constructionPrototype)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(recipePrototype.ID),
                ToolTip = recipePrototype.Name,
            };

            constructionButtons.Add(topLevelActionOption);
        }

        foreach (var categoryEntry in categoryEntries)
        {
            if (categoryStack.Contains(categoryEntry) ||
                !_prototype.TryIndex(categoryEntry, out var categoryPrototype))
                continue;

            categoryStack.Add(categoryEntry);
            var nestedButtons = ConvertToButtons(
                categoryPrototype.ConstructionEntries,
                categoryPrototype.CategoryEntries,
                categoryStack);
            categoryStack.Remove(categoryEntry);

            categoryButtons.Add((categoryPrototype, nestedButtons));
        }

        var models = new List<RadialMenuOptionBase>(categoryButtons.Count + constructionButtons.Count);

        foreach (var (categoryPrototype, buttonList) in categoryButtons)
        {
            models.Add(new RadialMenuNestedLayerOption(buttonList)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(categoryPrototype.Icon),
                ToolTip = categoryPrototype.Name == string.Empty
                    ? categoryPrototype.ID
                    : Loc.GetString(categoryPrototype.Name),
            });
        }

        models.AddRange(constructionButtons);
        return models;
    }

    private void HandlePlacement(ConstructionPrototype prototype)
    {
        var constructionSystem = _constructionSystem;
        if (constructionSystem == null)
            return;

        if (prototype.Type == ConstructionType.Item)
        {
            constructionSystem.TryStartItemConstruction(prototype.ID);
            _menu?.Close();
            return;
        }

        _placement.BeginPlacing(new PlacementInformation
            {
                IsTile = false,
                PlacementOption = prototype.PlacementMode,
            },
            new ConstructionPlacementHijack(constructionSystem, prototype));

        _menu?.Close();
    }
}

using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Research.Artifact.Effects.RandomTransformation;

/// <summary>
/// Handles the artifact effect that replaces nearby items with safe random prototypes.
/// </summary>
public sealed partial class ArtifactRandomTransformationSystem : BaseXAESystem<ArtifactRandomTransformationComponent>
{
    /*
     * Entry-point and lifecycle part of the system.
     */

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly HashSet<Entity<ItemComponent>> _items = [];
    private readonly HashSet<Entity<InventoryComponent>> _inventories = [];
    private readonly List<EntityUid> _inventoryItems = [];
    private readonly List<EntityUid> _worldItems = [];
    private readonly List<EntityPrototype> _baseCandidatePool = [];
    private readonly Dictionary<string, List<EntityPrototype>> _candidateCache = [];
    private bool _enabled = SunriseCCVars.ArtifactRandomTransformationEnabled.DefaultValue;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(
            SunriseCCVars.ArtifactRandomTransformationEnabled,
            OnArtifactRandomTransformationEnabledChanged,
            true);

        SubscribeLocalEvent<ArtifactRandomTransformationComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        RebuildCandidateCaches();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(SunriseCCVars.ArtifactRandomTransformationEnabled,
            OnArtifactRandomTransformationEnabledChanged);
    }

    protected override void OnActivated(Entity<ArtifactRandomTransformationComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        TryActivateTransformation(ent);
    }

    private bool TryActivateTransformation(Entity<ArtifactRandomTransformationComponent> ent)
    {
        if (!_enabled)
            return false;

        if (!TryGetTransformCandidates(ent, out var candidates))
            return false;

        var coords = Transform(ent).Coordinates;

        _items.Clear();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Radius, _items);
        CopyNearbyItems();

        SearchPlayersInventoryForItems(coords, ent.Comp.Radius);

        TryTransformItems(_inventoryItems, ent.Comp.TransformationPercentRatio, candidates);
        TryTransformItems(_worldItems, ent.Comp.TransformationPercentRatio, candidates);
        return true;
    }

    private void OnArtifactRandomTransformationEnabledChanged(bool newValue)
    {
        _enabled = newValue;
    }
}

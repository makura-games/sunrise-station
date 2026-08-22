using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Cuffs;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared.Maps;
using Content.Shared.Movement.Systems;
using Content.Shared.Roles;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.FleshCult;

public sealed partial class FleshCultSystem : EntitySystem
{
    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private StoreSystem _store = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SunriseHumanoidBodySystem _sunriseBody = default!;
    [Dependency] private SharedAppearanceSystem _sharedAppearance = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private GunSystem _gunSystem = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private CuffableSystem _cuffable = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private SharedJitteringSystem _jittering = default!;
    [Dependency] private SharedStutteringSystem _stuttering = default!;
    [Dependency] private ExplosionSystem _explosionSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private ContainerSystem _containerSystem = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private RoundEndSystem _roundEndSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MapSystem _mapSystem = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;

    private readonly List<string> _speciesWhitelist =
    [
        "Human",
        "Reptilian",
        "Dwarf",
        "Vulpkanin",
        "Felinid",
        "Moth",
        "Swine",
        "Arachnid",
        "Demon",
        "Vox",
        "HumanoidXeno",
        "Predator",
        "Tajaran",
        "Milira"
    ];

    public override void Initialize()
    {
        base.Initialize();

        InitializeVirus();
        InitializeAbilities();
        InitializeCultist();
        InitializeMob();
        InitializeHugger();
        InitializeHeart();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateCultist(frameTime);
        UpdateHugger(frameTime);
        UpdateVirus(frameTime);
        UpdateHeart(frameTime);
    }
}


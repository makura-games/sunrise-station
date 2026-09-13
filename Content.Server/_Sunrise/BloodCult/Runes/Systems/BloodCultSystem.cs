using Content.Server._Sunrise.BloodCult.GameRule;
using Content.Server._Sunrise.BloodCult.Objectives.Systems;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Cuffs;
using Content.Server.DoAfter;
using Content.Server.Emp;
using Content.Server.EUI;
using Content.Server.Flash;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Mind;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Sunrise.BloodCult.Systems;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Roles;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.BloodCult.Runes.Systems
{
    public sealed partial class BloodCultSystem : EntitySystem
    {
        [Dependency] private SharedActionsSystem _actionsSystem = default!;
        [Dependency] private AlertsSystem _alertsSystem = default!;
        [Dependency] private SharedAudioSystem _audio = default!;
        [Dependency] private BloodCultRuleSystem _bloodCultRuleSystem = default!;
        [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
        [Dependency] private GibbingSystem _gibbing = default!;
        [Dependency] private ChatSystem _chat = default!;
        [Dependency] private ContainerSystem _containerSystem = default!;
        [Dependency] private CuffableSystem _cuffable = default!;
        [Dependency] private DamageableSystem _damageableSystem = default!;
        [Dependency] private SharedMapSystem _map = default!;
        [Dependency] private DoAfterSystem _doAfterSystem = default!;
        [Dependency] private EmpSystem _empSystem = default!;
        [Dependency] private EntityManager _entityManager = default!;
        [Dependency] private SharedStackSystem _stack = default!;
        [Dependency] private EuiManager _euiManager = default!;
        [Dependency] private FlammableSystem _flammableSystem = default!;
        [Dependency] private FlashSystem _flashSystem = default!;
        [Dependency] private GunSystem _gunSystem = default!;
        [Dependency] private HandsSystem _handsSystem = default!;
        [Dependency] private InventorySystem _inventorySystem = default!;
        [Dependency] private SharedPointLightSystem _lightSystem = default!;
        [Dependency] private EntityLookupSystem _lookup = default!;
        [Dependency] private IMapManager _mapMan = default!;
        [Dependency] private MetaDataSystem _metaDataSystem = default!;
        [Dependency] private MindSystem _mindSystem = default!;
        [Dependency] private PopupSystem _popupSystem = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private SharedRoleSystem _roleSystem = default!;
        [Dependency] private ItemSlotsSystem _slotsSystem = default!;
        [Dependency] private StatusEffectsSystem _statusEffectsSystem = default!;
        [Dependency] private SharedStunSystem _stunSystem = default!;
        [Dependency] private SharedStutteringSystem _stuttering = default!;
        [Dependency] private ITileDefinitionManager _tileDefinition = default!;
        [Dependency] private TileSystem _tileSystem = default!;
        [Dependency] private TransformSystem _transformSystem = default!;
        [Dependency] private TurfSystem _turf = default!;
        [Dependency] private UserInterfaceSystem _ui = default!;
        [Dependency] private SharedTransformSystem _xform = default!;
        [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
        [Dependency] private NavMapSystem _navMap = default!;
        [Dependency] private PullingSystem _pulling = default!;
        [Dependency] private KillCultistTargetsConditionSystem _cultistTargetsConditionSystem = default!;
        [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
        [Dependency] private EntityQuery<FlammableComponent> _flammableQuery = default!;

        [ValidatePrototypeId<StackPrototype>]
        private static string SteelStackPrototypeId = "Steel";

        [ValidatePrototypeId<StackPrototype>]
        private static string PlasteelStackPrototypeId = "Plasteel";

        [ValidatePrototypeId<ContentTileDefinition>]
        private static string CultTilePrototypeId = "CultFloor";

        private static EntProtoId AirlockGlassCultPrototypeId = "AirlockGlassCult";
        private static EntProtoId ConstructShellPrototypeId = "ConstructShell";
        private static EntProtoId ApocalypseRunePrototypeId = "ApocalypseRune";
        private static EntProtoId RunicMetalPrototypeId = "CultRunicMetal";
        private static EntProtoId CultBarrierPrototypeId = "CultBarrier";
        private static EntProtoId CultBloodSpeelPrototypeId = "CultBloodSpell";
        private static EntProtoId TeleportInEffect = "CultTeleportInEffect";
        private static EntProtoId TeleportOutEffect = "CultTeleportOutEffect";
        private static EntProtoId HelmetPrototypeId = "ClothingHeadHelmetCult";
        private static EntProtoId ArmorPrototypeId = "CultOuterArmor";
        private static EntProtoId ShoesPrototypeId = "ClothingShoesCult";
        private static EntProtoId BolaPrototypeId = "CultBola";
        private static EntProtoId CuffsPrototypeId = "CultistCuffs";
        private static EntProtoId TeleportActionPrototypeId = "ActionCultTeleport";
        private static EntProtoId TwistedConstructionActionPrototypeId = "ActionCultTwistedConstruction";
        private static EntProtoId CultTileEffectPrototypeId = "CultTileSpawnEffect";
        public static EntProtoId ReaperConstructPrototypeId = "ReaperConstruct";
        public static EntProtoId AirlockConvertEffect = "CultAirlockGlow";

        private readonly SoundPathSpecifier _teleportInSound = new("/Audio/_Sunrise/BloodCult/veilin.ogg");
        private readonly SoundPathSpecifier _teleportOutSound = new("/Audio/_Sunrise/BloodCult/veilout.ogg");
        private readonly SoundPathSpecifier _apocRuneEndDrawing = new("/Audio/_Sunrise/BloodCult/finisheddraw.ogg");
        private readonly SoundPathSpecifier _apocRuneStartDrawing = new("/Audio/_Sunrise/BloodCult/startdraw.ogg");
        private readonly SoundPathSpecifier _narsie40Sec = new("/Audio/_Sunrise/BloodCult/40sec.ogg");
        private readonly SoundPathSpecifier _magic = new("/Audio/_Sunrise/BloodCult/magic.ogg");

        private bool _doAfterAlreadyStarted;

        private EntityUid? _playingStream;

        private HashSet<EntityUid> _intersectingEntities = new();

        private float _timeToDraw;

        public override void Initialize()
        {
            base.Initialize();

            InitializeBuffSystem();
            InitializeSoulShard();
            InitializeConstructs();
            InitializeBarrierSystem();
            InitializeConstructsAbilities();
            InitializeActions();
            InitializeRunes();
        }
    }
}

using System.Linq;
using Content.Server._Sunrise.NewLife;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.PlanetPrison
{
    public sealed partial class PlanetPrisonSystem : EntitySystem
    {
        [Dependency] private MindSystem _mindSystem = default!;
        [Dependency] private SharedRoleSystem _roleSystem = default!;
        [Dependency] private GameTicker _gameTicker = default!;
        [Dependency] private TransformSystem _transformSystem = default!;
        [Dependency] private NewLifeSystem _newLifeSystem = default!;
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private ISharedPlayerManager _player = default!;

        [ValidatePrototypeId<EntityPrototype>]
        private const string MindRole = "MindRolePlanetPrisoner";
        [ValidatePrototypeId<EntityPrototype>]
        private const string EscapeObjective = "PlanetPrisonerEscapeObjective";
        [ValidatePrototypeId<EntityPrototype>]
        private const string StayFreeObjective = "PlanetPrisonerStayFreeObjective";
        [ValidatePrototypeId<EntityPrototype>]
        private const string GameRule = "PlanetPrison";

        private const float EscapeDistance = 150f;

        public TimeSpan NextTick = TimeSpan.Zero;
        public TimeSpan RefreshCooldown = TimeSpan.FromSeconds(5);

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PlanetPrisonerComponent, MindAddedMessage>(OnMindAdded);
            SubscribeLocalEvent<PlanetPrisonRuleComponent, ObjectivesTextGetInfoEvent>(OnObjectivesTextGetInfo);
            SubscribeLocalEvent<PlanetPrisonRuleComponent, ObjectivesTextPrependEvent>(OnObjectivesTextPrepend);
            SubscribeLocalEvent<PlanetPrisonerRoleComponent, GetBriefingEvent>(OnGetBriefing);
        }

        private void OnGetBriefing(EntityUid uid, PlanetPrisonerRoleComponent component, ref GetBriefingEvent args)
        {
            if (!TryComp<MindComponent>(uid, out var mind) || mind.OwnedEntity == null)
                return;
            if (HasComp<PlanetPrisonerRoleComponent>(uid))
                return;
            args.Append(Loc.GetString("planet-prisoner-role-greeting"));
        }

        private void OnObjectivesTextPrepend(EntityUid uid, PlanetPrisonRuleComponent comp, ref ObjectivesTextPrependEvent args)
        {
            var planetPrisonRule = GetPlanetPrisonRule();
            if (planetPrisonRule == null)
                return;
            args.Text += Loc.GetString("planet-prison-round-end-result", ("count", planetPrisonRule.EscapedPrisoners.Count));
        }

        private void OnObjectivesTextGetInfo(EntityUid uid, PlanetPrisonRuleComponent comp, ref ObjectivesTextGetInfoEvent args)
        {
            args.Minds = comp.PrisonersMinds;
            args.AgentName = Loc.GetString("planet-prisoner-round-end-name");
        }

        public override void Update(float frameTime)
        {
            if (NextTick > _gameTiming.CurTime)
                return;

            NextTick += RefreshCooldown;

            var planetPrisonRule = GetPlanetPrisonRule();
            if (planetPrisonRule == null)
                return;

            var planetPrisonStation = EntityQuery<PlanetPrisonStationComponent>().FirstOrDefault();
            if (planetPrisonStation == null || planetPrisonStation.PrisonGrid == EntityUid.Invalid)
                return;
            var xformPrison = Transform(planetPrisonStation.PrisonGrid);
            var prisonPosition = _transformSystem.GetMapCoordinates(xformPrison);

            base.Update(frameTime);
            var query = AllEntityQuery<PlanetPrisonerComponent, TransformComponent>();
            while (query.MoveNext(out var prisoner, out _, out var xform))
            {
                var prisonerPosition = _transformSystem.GetMapCoordinates(xform);
                if (!_mindSystem.TryGetMind(prisoner, out var mindId, out var mind))
                {
                    continue;
                }

                if (prisonerPosition.MapId != planetPrisonStation.MapId)
                {
                    // А вот нехуй играть не по правилам
                    QueueDel(prisoner);
                    continue;
                }

                var distance = (prisonerPosition.Position - prisonPosition.Position).Length();

                if (!(distance > EscapeDistance))
                    continue;

                if (planetPrisonRule.EscapedPrisoners.Contains(mindId))
                    continue;

                // Не ну а чо
                QueueDel(prisoner);
                planetPrisonRule.EscapedPrisoners.Add(mindId);
                if (_player.TryGetSessionById(mind.UserId, out var session))
                {
                    _newLifeSystem.SetNextAllowRespawn(session.UserId, _gameTiming.CurTime);
                }
            }
        }

        public PlanetPrisonRuleComponent? GetPlanetPrisonRule()
        {
            var planetPrisonRule = EntityQuery<PlanetPrisonRuleComponent>().FirstOrDefault();
            return planetPrisonRule;
        }

        public PlanetPrisonRuleComponent StartPlanetPrisonRule()
        {
            _gameTicker.StartGameRule(GameRule, out var ruleEntity);
            var planetPrisonRule = Comp<PlanetPrisonRuleComponent>(ruleEntity);

            return planetPrisonRule;
        }

        public bool PrisonerEscaped(EntityUid mind)
        {
            var planetPrisonRule = GetPlanetPrisonRule();
            if (planetPrisonRule == null)
                return false;
            return planetPrisonRule.EscapedPrisoners.Contains(mind);
        }

        private void OnMindAdded(EntityUid uid, PlanetPrisonerComponent component, MindAddedMessage args)
        {
            var planetPrisonRule = GetPlanetPrisonRule() ?? StartPlanetPrisonRule();

            if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            {
                return;
            }

            if (component.FirstMindAdded)
                return;

            component.FirstMindAdded = true;


            if (_roleSystem.MindHasRole<PlanetPrisonerRoleComponent>(mindId))
            {
                _roleSystem.MindRemoveRole<PlanetPrisonerRoleComponent>(mindId);
            }

            _roleSystem.MindAddRole(mindId, MindRole);

            _mindSystem.TryAddObjective(mindId, mind, EscapeObjective);
            _mindSystem.TryAddObjective(mindId, mind, StayFreeObjective);

            planetPrisonRule.PrisonersMinds.Add((mindId, Name(uid)));
        }
    }
}

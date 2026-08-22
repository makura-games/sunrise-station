using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Fax;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Shared.Fax.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.Fugitive
{
    public sealed partial class FugitiveSystem : EntitySystem
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
        [Dependency] private IGameTiming _timing = default!;
        [Dependency] private ChatSystem _chat = default!;
        [Dependency] private MindSystem _mindSystem = default!;
        [Dependency] private FaxSystem _faxSystem = default!;
        [Dependency] private SharedRoleSystem _roleSystem = default!;
        [Dependency] private GameTicker _gameTicker = default!;

        [ValidatePrototypeId<EntityPrototype>]
        private const string MindRole = "MindRoleFugitive";
        [ValidatePrototypeId<EntityPrototype>]
        private const string EscapeObjective = "FugitiveEscapeShuttleObjective";
        [ValidatePrototypeId<EntityPrototype>]
        private const string GameRule = "Fugitive";

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<FugitiveComponent, MindAddedMessage>(OnMindAdded);

            SubscribeLocalEvent<FugitiveRuleComponent, ObjectivesTextGetInfoEvent>(OnObjectivesTextGetInfo);
            SubscribeLocalEvent<FugitiveRoleComponent, GetBriefingEvent>(OnGetBriefing);
        }

        private void OnGetBriefing(EntityUid uid, FugitiveRoleComponent component, ref GetBriefingEvent args)
        {
            if (!TryComp<MindComponent>(uid, out var mind) || mind.OwnedEntity == null)
                return;
            if (HasComp<FugitiveRoleComponent>(uid)) // don't show both briefings
                return;
            args.Append(Loc.GetString("fugitive-role-greeting"));
        }

        private void OnObjectivesTextGetInfo(EntityUid uid, FugitiveRuleComponent comp, ref ObjectivesTextGetInfoEvent args)
        {
            args.Minds = comp.FugitiveMinds;
            args.AgentName = Loc.GetString("fugitive-round-end-name");
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var (cd, _) in EntityQuery<FugitiveCountdownComponent, _Sunrise.Fugitive.FugitiveComponent>())
            {
                if (cd.AnnounceTime == null || !(_timing.CurTime > cd.AnnounceTime))
                    continue;
                _chat.DispatchGlobalAnnouncement(Loc.GetString("station-event-fugitive-hunt-announcement"), sender: Loc.GetString("fugitive-announcement-GALPOL"), colorOverride: Color.Yellow);

                SendFugiReport(cd.Owner);

                RemCompDeferred<FugitiveCountdownComponent>(cd.Owner);
            }
        }

        public bool SendFugiReport(EntityUid fugitive)
        {
            var report = GenerateFugiReport(fugitive);
            var faxes = EntityQuery<FaxMachineComponent>();
            var wasSent = false;
            foreach (var fax in faxes)
            {
                if (!fax.ReceiveNukeCodes)
                {
                    continue;
                }

                var printout = new FaxPrintout(
                    report.ToString(),
                    Loc.GetString("fugi-report-ent-name", ("name", fugitive)),
                    null,
                    null,
                    "paper_stamp-centcom",
                    new List<StampDisplayInfo>
                    {
                        new StampDisplayInfo { StampedName = Loc.GetString("stamp-component-stamped-name-centcom"), StampedColor = Color.FromHex("#BB3232") },
                    });
                _faxSystem.Receive(fax.Owner, printout, null, fax);

                wasSent = true;
            }

            return wasSent;
        }

        public FugitiveRuleComponent GetFugitiveRule()
        {
            var planetPrisonRule = EntityQuery<FugitiveRuleComponent>().FirstOrDefault();
            if (planetPrisonRule != null)
                return planetPrisonRule;

            _gameTicker.StartGameRule(GameRule, out var ruleEntity);
            planetPrisonRule = Comp<FugitiveRuleComponent>(ruleEntity);

            return planetPrisonRule;
        }

        private void OnMindAdded(EntityUid uid, FugitiveComponent component, MindAddedMessage args)
        {
            var fugitiveRule = GetFugitiveRule();

            if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            {
                return;
            }

            if (component.FirstMindAdded)
                return;

            component.FirstMindAdded = true;


            if (_roleSystem.MindHasRole<FugitiveRoleComponent>(mindId))
            {
                _roleSystem.MindRemoveRole<FugitiveRoleComponent>(mindId);
            }

            _roleSystem.MindAddRole(mindId, MindRole);

            _mindSystem.TryAddObjective(mindId, mind, EscapeObjective);

            fugitiveRule.FugitiveMinds.Add((mindId, Name(uid)));

            _movementSpeed.RefreshMovementSpeedModifiers(uid);
        }

        private FormattedMessage GenerateFugiReport(EntityUid uid)
        {
            FormattedMessage report = new();
            report.AddMarkup(Loc.GetString("fugi-report-title", ("name", uid)));
            report.PushNewline();
            report.PushNewline();
            report.AddMarkup(Loc.GetString("fugitive-report-first-line", ("name", uid)));
            report.PushNewline();
            report.PushNewline();


            if (!TryComp<HumanoidProfileComponent>(uid, out var humanoidComponent) ||
                !_prototypeManager.TryIndex(humanoidComponent.Species, out var species))
            {
                report.AddMarkup(Loc.GetString("fugitive-report-inhuman", ("name", uid)));
                return report;
            }

            report.AddMarkup(Loc.GetString("fugitive-report-morphotype", ("species", Loc.GetString(species.Name))));
            report.PushNewline();
            report.AddMarkup(Loc.GetString("fugitive-report-age", ("age", humanoidComponent.Age)));
            report.PushNewline();

            string sexLine = string.Empty;
            sexLine += humanoidComponent.Sex switch
            {
                Sex.Male => Loc.GetString("fugitive-report-sex-m"),
                Sex.Female => Loc.GetString("fugitive-report-sex-f"),
                _ => Loc.GetString("fugitive-report-sex-n")
            };

            report.AddMarkup(sexLine);

            if (TryComp<PhysicsComponent>(uid, out var physics))
            {
                report.PushNewline();
                report.AddMarkup(Loc.GetString("fugitive-report-weight", ("weight", Math.Round(physics.FixturesMass))));
            }
            report.PushNewline();
            report.PushNewline();
            report.AddMarkup(Loc.GetString("fugitive-report-last-line"));

            return report;
        }
    }
}

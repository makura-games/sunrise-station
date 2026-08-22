using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Content.Shared._Sunrise.CopyMachine;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.StationGoal
{
    public sealed partial class StationGoalPaperSystem : EntitySystem
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private ContainerSystem _containerSystem = default!;
        [Dependency] private PaperSystem _paperSystem = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private IRobustRandom _random = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarting);
        }

        private void OnRoundStarting(RoundStartedEvent ev)
        {
            var playerCount = _playerManager.PlayerCount;

            var query = EntityQueryEnumerator<StationGoalComponent>();
            while (query.MoveNext(out var uid, out var station))
            {
                var tempGoals = new List<ProtoId<StationGoalPrototype>>(station.Goals);
                _random.Shuffle(tempGoals);

                StationGoalPrototype? selGoal = null;
                foreach (var goalId in tempGoals)
                {
                    var goalProto = _prototypeManager.Index(goalId);

                    if (playerCount < goalProto.MinPlayers)
                        continue;

                    if (playerCount > goalProto.MaxPlayers)
                        continue;

                    selGoal = goalProto;
                    break;
                }

                if (selGoal is null)
                    continue;

                if (SendStationGoal(uid, selGoal))
                    Log.Info($"Goal {selGoal.ID} has been sent to station {MetaData(uid).EntityName}");
            }
        }

        public bool SendStationGoal(EntityUid? ent, ProtoId<StationGoalPrototype> goal)
            => SendStationGoal(ent, _prototypeManager.Index(goal));

        public bool SendStationGoal(EntityUid? ent, StationGoalPrototype goal)
        {
            if (ent is null)
                return false;

            var wasSent = false;

            SpriteSpecifier? header = null;
            var poolId = _cfg.GetCVar(SunriseCCVars.DocumentTemplatePool);
            if (_prototypeManager.TryIndex<DocTemplatePoolPrototype>(poolId, out var poolProto))
                header = poolProto.StationGoalHeader;

            var printout = new FaxPrintout(
                Loc.GetString(goal.Text, ("station", MetaData(ent.Value).EntityName)),
                Loc.GetString("station-goal-fax-paper-name"),
                null,
                null,
                "paper_stamp-centcom",
                new List<StampDisplayInfo>
                {
                    new() { StampedName = Loc.GetString("stamp-component-stamped-name-centcom"), StampedColor = Color.Green },
                },
                imageContent: header);

            var faxQuery = EntityQueryEnumerator<FaxMachineComponent>();
            while (faxQuery.MoveNext(out var faxId, out var fax))
            {
                if (!fax.ReceiveStationGoal)
                    continue;

                var xform = Transform(faxId);
                var paper = SpawnPaperGoal(xform.Coordinates, printout);
                var lockbox = Spawn(goal.LockBoxPrototypeId, xform.Coordinates);
                if (_containerSystem.TryGetContainer(lockbox, "storagebase", out var container))
                {
                    _containerSystem.Insert(paper, container);
                    if (goal.ExtraItems.Count != 0)
                    {
                        foreach (var goalExtraItem in goal.ExtraItems)
                        {
                            var item = Spawn(goalExtraItem);
                            _containerSystem.Insert(item, container);
                        }
                    }
                }

                wasSent = true;
            }

            return wasSent;
        }

        private EntityUid SpawnPaperGoal(EntityCoordinates coords, FaxPrintout printout)
        {
            var entityToSpawn = printout.PrototypeId.Length == 0 ? "Paper" : printout.PrototypeId;
            var printed = Spawn(entityToSpawn, coords);
            if (!TryComp<PaperComponent>(printed, out var paper))
                return printed;

            _paperSystem.SetContent((printed, paper), printout.Content);
            if (printout.ImageContent != null)
                _paperSystem.SetImageContent((printed, paper), printout.ImageContent, printout.ImageScale);

            if (printout.StampState == null)
                return printed;

            foreach (var stamp in printout.StampedBy)
                _paperSystem.TryStamp((printed, paper), stamp, printout.StampState);

            return printed;
        }
    }
}

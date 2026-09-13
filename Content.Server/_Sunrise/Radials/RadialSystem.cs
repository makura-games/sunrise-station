using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared._Sunrise.Radials;
using Content.Shared._Sunrise.Radials.Systems;

namespace Content.Server._Sunrise.Radials;

public sealed partial class RadialSystem : SharedRadialSystem
    {
        [Dependency] private ISharedAdminLogManager _adminLogger = default!;
        [Dependency] private PopupSystem _popupSystem = default!;
        [Dependency] private IAdminManager _adminMgr = default!;
        [Dependency] private HandsSystem _handsSystem = default!;

        private ISawmill _sawmill = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeNetworkEvent<RequestServerRadialsEvent>(HandleRadialRequest);
        }

        private void HandleRadialRequest(RequestServerRadialsEvent args, EntitySessionEventArgs eventArgs)
        {
            var player = eventArgs.SenderSession;
            var playerEntity = GetEntity(args.EntityUid);

            if (!Exists(playerEntity))
            {
                _sawmill.Warning($"{nameof(HandleRadialRequest)} called on a non-existent entity with id {args.EntityUid} by player {player}.");
                return;
            }

            if (player.AttachedEntity is not {} attached)
            {
                _sawmill.Warning($"{nameof(HandleRadialRequest)} called by player {player} with no attached entity.");
                return;
            }

            // We do not verify that the user has access to the requested entity. The individual verbs should check
            // this, and some verbs (e.g. view variables) won't even care about whether an entity is accessible through
            // the entity menu or not.

            var force = args.AdminRequest && _adminMgr.HasAdminFlag(eventArgs.SenderSession, AdminFlags.Admin);

            HashSet<Type> radialsTypes = new();
            foreach (var key in args.RadialTypes)
            {
                var type = Radial.RadialTypes.FirstOrDefault(x => x.Name == key);

                if (type != null)
                    radialsTypes.Add(type);
                else
                    _sawmill.Error($"Unknown verb type received: {key}");
            }

            var response =
                new RadialsResponseEvent(args.EntityUid, GetLocalRadials(playerEntity, attached, radialsTypes, force));

            RaiseNetworkEvent(response, player);
        }

        /// <summary>
        ///     Execute the provided verb.
        /// </summary>
        /// <remarks>
        ///     This will try to call the action delegates and raise the local events for the given verb.
        /// </remarks>
        public override void ExecuteRadial(Radial radial, EntityUid user, EntityUid target, bool forced = false)
        {
            // is this verb actually valid?
            if (radial.Disabled)
            {
                // Send an informative pop-up message
                if (!string.IsNullOrWhiteSpace(radial.Message))
                    _popupSystem.PopupEntity(radial.Message, user, user);

                return;
            }

            // first, lets log the verb. Just in case it ends up crashing the server or something.
            LogRadial(radial, user, target, forced);

            base.ExecuteRadial(radial, user, target, forced);
        }

        public void LogRadial(Radial radial, EntityUid user, EntityUid target, bool forced)
        {
            // first get the held item. again.
            EntityUid? holding = null;

            if (TryComp(user, out HandsComponent? hands))
            {
                holding = _handsSystem.GetActiveItem((user, hands));
            }

            // if this is a virtual pull, get the held entity
            if (holding != null && TryComp(holding, out VirtualItemComponent? pull))
                holding = pull.BlockingEntity;

            var verbText = $"{radial.Text}".Trim();

            // lets not frame people, eh?
            var executionText = forced ? "was forced to execute" : "executed";

            if (holding == null)
            {
                _adminLogger.Add(LogType.Verb, radial.Impact,
                        $"{ToPrettyString(user):user} {executionText} the [{verbText:verb}] verb targeting {ToPrettyString(target):target}");
            }
            else
            {
                _adminLogger.Add(LogType.Verb, radial.Impact,
                       $"{ToPrettyString(user):user} {executionText} the [{verbText:verb}] verb targeting {ToPrettyString(target):target} while holding {ToPrettyString(holding.Value):held}");
            }
        }
    }

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Examine;
using Content.Client.Gameplay;
using Content.Client.Popups;
using Content.Shared.Examine;
using Content.Shared.Tag;
using Content.Shared._Sunrise.Radials;
using Content.Shared._Sunrise.Radials.Systems;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Radials;

[UsedImplicitly]
public sealed partial class RadialSystem : SharedRadialSystem
{
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private ExamineSystem _examineSystem = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IEyeManager _eyeManager = default!;

    /// <summary>
    ///     When a user right clicks somewhere, how large is the box we use to get entities for the context menu?
    /// </summary>
    public const float EntityMenuLookupSize = 0.25f;

    /// <summary>
    ///     These flags determine what entities the user can see on the context menu.
    /// </summary>
    public MenuVisibility Visibility;

    private const string HideTag = "HideContextMenu";

    public Action<RadialsResponseEvent>? OnRadialsResponse;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RadialsResponseEvent>(HandleRadialsResponse);
    }

    /// <summary>
    ///     Get all of the entities in an area for displaying on the context menu.
    /// </summary>
    public bool TryGetEntityMenuEntities(MapCoordinates targetPos, [NotNullWhen(true)] out List<EntityUid>? result)
    {
        result = null;

        if (_stateManager.CurrentState is not GameplayStateBase gameScreenBase)
            return false;

        var player = _playerManager.LocalEntity;
        if (player == null)
            return false;

        // If FOV drawing is disabled, we will modify the visibility option to ignore visiblity checks.
        var visibility = _eyeManager.CurrentEye.DrawFov
            ? Visibility
            : Visibility | MenuVisibility.NoFov;

        // Get entities
        List<EntityUid> entities;

        // Do we have to do FoV checks?
        if ((visibility & MenuVisibility.NoFov) == 0)
        {
            var entitiesUnderMouse = gameScreenBase.GetClickableEntities(targetPos).ToHashSet();

            bool Predicate(EntityUid e) => e == player || entitiesUnderMouse.Contains(e);

            // first check the general location.
            if (!_examineSystem.CanExamine(player.Value, targetPos, Predicate))
                return false;

            TryComp(player.Value, out ExaminerComponent? examiner);

            // Then check every entity
            entities = new();
            foreach (var ent in _entityLookup.GetEntitiesInRange(targetPos, EntityMenuLookupSize))
            {
                if (_examineSystem.CanExamine(player.Value, targetPos, Predicate, ent, examiner))
                    entities.Add(ent);
            }
        }
        else
        {
            entities = _entityLookup.GetEntitiesInRange(targetPos, EntityMenuLookupSize).ToList();
        }

        if (entities.Count == 0)
            return false;

        if (visibility == MenuVisibility.All)
        {
            result = entities;
            return true;
        }

        // remove any entities in containers
        if ((visibility & MenuVisibility.InContainer) == 0)
        {
            for (var i = entities.Count - 1; i >= 0; i--)
            {
                var entity = entities[i];

                if (ContainerSystem.IsInSameOrTransparentContainer(player.Value, entity))
                    continue;

                entities.RemoveSwap(i);
            }
        }

        // remove any invisible entities
        if ((visibility & MenuVisibility.Invisible) == 0)
        {
            for (var i = entities.Count - 1; i >= 0; i--)
            {
                var entity = entities[i];

                if (!TryComp<SpriteComponent>(entity, out var sprite))
                    continue;

                if (!sprite.Visible || _tagSystem.HasTag(entity, HideTag))
                    entities.RemoveSwap(i);
            }
        }

        // Remove any entities that do not have LOS
        if ((visibility & MenuVisibility.NoFov) == 0)
        {
            var playerPos = _transform.GetMapCoordinates(player.Value);

            for (var i = entities.Count - 1; i >= 0; i--)
            {
                var entity = entities[i];

                if (!_examineSystem.InRangeUnOccluded(
                        playerPos,
                        _transform.GetMapCoordinates(entity),
                        ExamineSystemShared.ExamineRange,
                        null))
                {
                    entities.RemoveSwap(i);
                }
            }
        }

        if (entities.Count == 0)
            return false;

        result = entities;
        return true;
    }

    /// <summary>
    ///     Asks the server to send back a list of server-side verbs, for the given verb type.
    /// </summary>
    public SortedSet<Radial> GetRadials(EntityUid target, EntityUid user, Type type, bool force = false)
    {
        return GetRadials(target, user, new HashSet<Type>() { type }, force);
    }

    /// <summary>
    ///     Ask the server to send back a list of server-side verbs, and for now return an incomplete list of verbs
    ///     (only those defined locally).
    /// </summary>
    public SortedSet<Radial> GetRadials(
        EntityUid target,
        EntityUid user,
        HashSet<Type> verbTypes,
        bool force = false)
    {
        if (!IsClientSide(target))
            RaiseNetworkEvent(new RequestServerRadialsEvent(GetNetEntity(target), verbTypes, adminRequest: force));

        // Some admin menu interactions will try get verbs for entities that have not yet been sent to the player.
        if (!Exists(target))
            return new();

        return GetLocalRadials(target, user, verbTypes, force);
    }

    /// <summary>
    ///     Execute actions associated with the given verb.
    /// </summary>
    /// <remarks>
    ///     Unless this is a client-exclusive verb, this will also tell the server to run the same verb.
    /// </remarks>
    public void ExecuteRadial(EntityUid target, Radial radial)
    {
        var user = _playerManager.LocalEntity;
        if (user == null)
            return;

        // is this verb actually valid?
        if (radial.Disabled)
        {
            // maybe send an informative pop-up message.
            if (!string.IsNullOrWhiteSpace(radial.Message))
                _popupSystem.PopupEntity(radial.Message, user.Value);

            return;
        }

        if (radial.ClientExclusive || IsClientSide(target))
            ExecuteRadial(radial, user.Value, target);
        else
            RaisePredictiveEvent(new ExecuteRadialEvent(GetNetEntity(target), radial));
    }

    private void HandleRadialsResponse(RadialsResponseEvent msg)
    {
        OnRadialsResponse?.Invoke(msg);
    }
}

[Flags]
public enum MenuVisibility
{
    // What entities can a user see on the entity menu?
    Default = 0,          // They can only see entities in FoV.
    NoFov = 1 << 0,       // They ignore FoV restrictions
    InContainer = 1 << 1, // They can see through containers.
    Invisible = 1 << 2,   // They can see entities without sprites and the "HideContextMenu" tag is ignored.
    All = NoFov | InContainer | Invisible
}

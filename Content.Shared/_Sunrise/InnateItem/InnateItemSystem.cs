using Content.Shared.Actions;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.InnateItem;

public sealed partial class InnateItemSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private MetaDataSystem _metadata = default!;

    private static readonly EntProtoId InnateEntityTargetAction = "InnateEntityTargetAction";
    private static readonly EntProtoId InnateInstantActionAction = "InnateInstantActionAction";
    private const string InnateItemContainerId = "innate_items";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InnateItemComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<InnateItemComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<InnateItemComponent, InnateEntityTargetActionEvent>(EntityTargetActionActivate);
        SubscribeLocalEvent<InnateItemComponent, InnateInstantActionEvent>(InstantActionActivate);
        SubscribeLocalEvent<InnateItemComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(EntityUid uid, InnateItemComponent component, ComponentInit args)
    {
        var containerManager = EnsureComp<ContainerManagerComponent>(uid);
        _containerSystem.EnsureContainer<Container>(uid, InnateItemContainerId, containerManager);
    }

    private void OnShutdown(EntityUid uid, InnateItemComponent component, ComponentShutdown args)
    {
        foreach (var action in component.Actions)
        {
            _actionsSystem.RemoveAction(action);
        }

        if (_containerSystem.TryGetContainer(uid, InnateItemContainerId, out var container))
        {
            _containerSystem.CleanContainer(container);
        }

        component.Actions.Clear();
    }

    private void OnMapInit(EntityUid uid, InnateItemComponent component, MapInitEvent args)
    {
        AddItems(uid, component);
    }

    private void AddItems(EntityUid uid, InnateItemComponent component)
    {
        if (!_containerSystem.TryGetContainer(uid, InnateItemContainerId, out var container))
            return;

        // Обрабатываем worldTargetActions
        AddActionsFromPrototypes(uid, component, component.EntityTargetActions, container, InnateEntityTargetAction, true);

        // Обрабатываем instantActions
        AddActionsFromPrototypes(uid, component, component.InstantActions, container, InnateInstantActionAction, false);
    }

    /// <summary>
    /// Общий метод для добавления действий из списка прототипов
    /// </summary>
    private void AddActionsFromPrototypes(
        EntityUid uid,
        InnateItemComponent component,
        List<EntProtoId?> prototypeIds,
        BaseContainer container,
        EntProtoId actionPrototypeId,
        bool isEntityTarget)
    {
        foreach (var itemProto in prototypeIds)
        {
            if (itemProto == null)
                continue;

            var spawnedItem = Spawn(itemProto);

            if (TryComp<ActivatableUIComponent>(spawnedItem, out var activatableUIComponent))
            {
                activatableUIComponent.RequiresComplex = false;
                activatableUIComponent.InHandsOnly = false;
                activatableUIComponent.RequireActiveHand = false;
                Dirty(spawnedItem, activatableUIComponent);
            }

            _containerSystem.Insert(spawnedItem, container);

            var action = Spawn(actionPrototypeId);

            _actionsSystem.SetIcon(action,
                new SpriteSpecifier.EntityPrototype(MetaData(spawnedItem).EntityPrototype!.ID));

            // Устанавливаем соответствующий тип события в зависимости от типа действия
            if (isEntityTarget)
                _actionsSystem.SetEvent(action, new InnateEntityTargetActionEvent(spawnedItem));
            else
                _actionsSystem.SetEvent(action, new InnateInstantActionEvent(spawnedItem));

            _metadata.SetEntityName(action, MetaData(spawnedItem).EntityName);
            _metadata.SetEntityDescription(action, MetaData(spawnedItem).EntityDescription);

            _actionContainer.AddAction(uid, action);
            _actionsSystem.AddAction(uid, action, uid);
            component.Actions.Add(action);
        }
    }

    private void EntityTargetActionActivate(EntityUid uid, InnateItemComponent component, InnateEntityTargetActionEvent args)
    {
        _interactionSystem.InteractUsing(
            args.Performer,
            args.Item,
            args.Target,
            Transform(args.Target).Coordinates,
            false,
            false,
            false);
    }

    private void InstantActionActivate(EntityUid uid, InnateItemComponent component, InnateInstantActionEvent args)
    {
        var ev = new UseInHandEvent(args.Performer);
        RaiseLocalEvent(args.Item, ev);
    }
}

public sealed partial class InnateEntityTargetActionEvent : EntityTargetActionEvent
{
    public EntityUid Item;

    public InnateEntityTargetActionEvent(EntityUid item) : this()
    {
        Item = item;
    }
}

public sealed partial class InnateInstantActionEvent : InstantActionEvent
{
    public EntityUid Item;

    public InnateInstantActionEvent(EntityUid item) : this()
    {
        Item = item;
    }
}

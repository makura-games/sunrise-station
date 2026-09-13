// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt

using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Cloning.Events;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Pets;

public abstract partial class SharedPettingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    // Стандартный приказ, выдающийся при приручении
    private const PetOrderType DefaultOrder = PetOrderType.Follow;
    private const PetOrderType AttackOrder = PetOrderType.Attack;

    // Айди акшенов, которые будут выдаваться хозяину при появлении питомца.
    private static readonly EntProtoId OpenUiAction = "PetOpenAllUiAction";
    private static readonly EntProtoId AttackTargetAction = "PetAttackTargetAction";

    // Эффекты
    private static readonly EntProtoId PettingSuccessEffect = "EffectHearts";

    public override void Initialize()
    {
        base.Initialize();

        // Приручение
        SubscribeLocalEvent<PetOnInteractComponent, BeforeInteractHandEvent>(OnPetAttempt);

        // Управление
        SubscribeLocalEvent<PetOnInteractComponent, PetOpenAllUiEvent>(OnPetUiActionToggled);
        SubscribeLocalEvent<PetOnInteractComponent, PetAttackTargetEvent>(OnPetAttackToggled);

        // Совместимость
        SubscribeLocalEvent<PetOnInteractComponent, CloningEvent>(OnMasterCloned);
        SubscribeLocalEvent<PetOnInteractComponent, ComponentShutdown>(OnMasterShutdown);
        SubscribeLocalEvent<PettableOnInteractComponent, ComponentShutdown>(OnPetShutdown);

        SubscribeLocalEvent<PettableOnInteractComponent, LoadoutPetSpawned>(OnLoadoutSpawn);

    }

    #region Base petting

    private void OnPetAttempt(Entity<PetOnInteractComponent> master, ref BeforeInteractHandEvent args)
    {
        // Эта проверка позволяет методу срабатывать на клиенте только в первый раз, когда он предугадывает его.
        // Нужно, чтобы клиент 999 раз не выполнял действия ниже
        if (!_timing.IsFirstTimePredicted)
            return;

        // Проверяем возможность приручения у таргета.
        if (!TryComp<PettableOnInteractComponent>(args.Target, out var pet))
            return;

        // Проверяем, спит ли таргет
        if (HasComp<SleepingComponent>(args.Target))
            return;

        // Проверяем, жив ли таргет.
        if (!_mobState.IsAlive(args.Target))
            return;

        // Объявляем перменные, чтобы несколько раз не передавать ее длинную расшифровку.
        var petEntity = (args.Target, pet);

        // Пытаемся задать питомцу хозязина и проверяем, получилось ли.
        if (!TrySetMaster(petEntity, master))
            return;

        // Приручаем питомца, если все прошло успешно и код дошел до этого момента.
        Pet(petEntity);
    }

    /// <summary>
    /// Задает владельца питомца и вызывает ивент, сообщающий об этом
    /// </summary>
    /// <param name="pet">Питомец</param>
    /// <param name="master">Владелец</param>
    /// <param name="overrideMaster">Следует ли перезаписать текущего владельца, если он есть?</param>
    public bool TrySetMaster(Entity<PettableOnInteractComponent> pet, Entity<PetOnInteractComponent> master, bool overrideMaster = false)
    {
        // По умолчанию сменить владельца после приручения нельзя
        if (pet.Comp.Master != null && !overrideMaster)
            return false;

        // Меняем владельца
        pet.Comp.Master = master;
        Dirty(pet);

        // Добавляем питомца в список прирученных питомцев
        master.Comp.Pets.Add(pet);
        Dirty(master);

        // Вызываем ивент, который сообщает о смене владельца
        RaiseLocalEvent(pet, new PetMasterChanged(master));

        return true;
    }

    /// <summary>
    /// Метод, работающий с логикой отвязывания хозяина от питомца
    /// </summary>
    /// <param name="pet">Питомец</param>
    /// <param name="master">Хозяин</param>
    private void RemoveMaster(Entity<PettableOnInteractComponent> pet, Entity<PetOnInteractComponent> master)
    {
        // Заставляем питомца забыть своего хозяина
        pet.Comp.Master = null;
        Dirty(pet);

        // Заставляем хозяина забыть питомца
        MasterForgetPet(master, pet);
    }

    /// <summary>
    /// Метод, убирающий питомца из списка питомцев хозяина.
    /// </summary>
    /// <param name="master">Ентити хозяина, у которого мы убираем питомца</param>
    /// <param name="pet">EntityUid питомца, которого мы убираем</param>
    private void MasterForgetPet(Entity<PetOnInteractComponent> master, EntityUid pet)
    {
        // Убираем питомца из списка прирученных питомцев у хозяина
        master.Comp.Pets.Remove(pet);
        Dirty(master);

        // Если после отвязки у хозяина не осталось питомцев...
        if (master.Comp.Pets.Count != 0)
            return;

        // ..Мы убираем у него все акшены, связанные с питомцами.
        foreach (var actionUid in master.Comp.PetActions)
        {
            _actions.RemoveAction(actionUid);
        }

        // TODO: Возможность убрать акшен атаки отдельно, когда не осталось питомцев с возможностью атаковать.
    }

    /// <summary>
    /// Работает с логикой поведения питомца после приручения
    /// </summary>
    /// <param name="pet">Питомец</param>
    /// <param name="silent">Скрывать визуальные эффекты, звуки и попапы?</param>
    public void Pet(Entity<PettableOnInteractComponent> pet, bool silent = false)
    {
        // Проигрываем звук приручения, если он есть
        if (pet.Comp.PettingSuccessfulSound != null && !silent)
            _audio.PlayEntity(pet.Comp.PettingSuccessfulSound, pet, pet);

        // Необходимая проверка, чтобы еще раз убедиться, что хозяин имеется. Даже с учетом того, что он не может не иметься
        // Так как метод вызывается после присваивания хозяина.
        var master = pet.Comp.Master;
        if (!master.HasValue)
            return;

        // Показываем игроку попап об успешном приручении
        if (!silent)
        {
            var message = Loc.GetString("pet-success",
                ("name", Identity.Name(pet, EntityManager, master.Value)));

            _popup.PopupClient(message, pet.Owner, master.Value);
        }


        // Спавним эффект с сердечками
        if (!silent)
            Spawn(PettingSuccessEffect, _transform.GetMapCoordinates(pet));

        // Если питомцу не доступен стандартный приказ, то он не меняет своей логики поведения
        if (!pet.Comp.AllowedOrders.Contains(DefaultOrder))
            return;

        // Ивент информирует, что питомцу требуется обновление логики НПС. А она только на сервере
        RaiseLocalEvent(pet, new PetSetAILogicEvent(DefaultOrder));

        // Дальше добавляем акшены для управления питомцем.
        // Делаем это через код, чтобы они появлялись только после приручения питомца

        if (!TryComp<PetOnInteractComponent>(master, out var masterComponent))
            return;

        var masterEntity = (master.Value, masterComponent);

        TryAddOpenUiAction(masterEntity);
        TryAddAttackAction(masterEntity);
    }

    /// <summary>
    /// Добавляет акшен открывающий интерфейсы питомцев.
    /// Работает только 1 раз, после первого приручения
    /// </summary>
    /// <param name="master">Entity(PetOnInteractComponent) его хозяина</param>
    private bool TryAddOpenUiAction(Entity<PetOnInteractComponent> master)
    {
        // Если питомцев больше одного - это не первое приручение и акшен уже имеется, разворачиваемся
        if (master.Comp.Pets.Count != 1)
            return false;

        // Добавляем акшен хозяину и добавляем его в список акшенов
        var action = _actions.AddAction(master, OpenUiAction);

        if (!action.HasValue)
            return false;

        master.Comp.PetActions.Add(action.Value);
        return true;
    }

    /// <summary>
    /// Добавляет акшен выбирающий питомцам цель атаки.
    /// Работает только 1 раз, после добавления питомца с возможностью атаковать.
    /// </summary>
    /// <param name="master">Entity(PetOnInteractComponent) его хозяина</param>
    private bool TryAddAttackAction(Entity<PetOnInteractComponent> master)
    {
        // Список всех питомцев, имеющих возможность атаковать
        var aggressivePetList =
            master.Comp.Pets.Where(x => TryComp<PettableOnInteractComponent>(x, out var petComponent)
                                   && petComponent.AllowedOrders.Contains(PetOrderType.Attack));

        // Если таких питомцев больше одного - это не первое приручение и акшен уже имеется, разворачиваемся
        if (aggressivePetList.Count() != 1)
            return false;

        // Добавляем акшен хозяину и добавляем его в список акшенов
        var action = _actions.AddAction(master, AttackTargetAction);

        if (!action.HasValue)
            return false;

        master.Comp.PetActions.Add(action.Value);
        return true;
    }

    private void OnLoadoutSpawn(Entity<PettableOnInteractComponent> pet, ref LoadoutPetSpawned args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!TryComp<PetOnInteractComponent>(args.Master, out var masterComponent))
            return;

        var masterEntity = (args.Master, masterComponent);

        // Пытаемся задать питомцу хозязина и проверяем, получилось ли.
        if (!TrySetMaster(pet, masterEntity))
            return;

        // Приручаем питомца, если все прошло успешно и код дошел до этого момента.
        Pet(pet, true);
    }

    #endregion

    #region Petting events

    /// <summary>
    /// Метод, вызываем при активации акшена хозяина питомцев.
    /// Акшен должен переключать меню управления всех питомцев хозяина для удобства управления несколькими питомцами.
    /// </summary>
    /// <param name="master">Entity хозяина</param>
    /// <param name="args">Ивент, вызываемый акшеном, типа PetOpenAllUiEvent</param>
    private void OnPetUiActionToggled(Entity<PetOnInteractComponent> master, ref PetOpenAllUiEvent args)
    {
        // Выполняем действия для каждого питомца из списка прирученных питомцев у хозяина.
        foreach (var pet in master.Comp.Pets)
        {
            _ui.TryToggleUi(pet, PetControlUiKey.Key, master);
        }
    }

    /// <summary>
    /// Метод, работающий с каждым питомцем, задавая ему приказ атаковать, если это возможно.
    /// </summary>
    /// <param name="master">Entity хозяина</param>
    /// <param name="args">Ивент, передающий приказ атаковать и таргет для атаки, типа PetAttackTargetEvent</param>
    private void OnPetAttackToggled(Entity<PetOnInteractComponent> master, ref PetAttackTargetEvent args)
    {
        // Проходимся по всем прирученным питомцам у владельца
        foreach (var pet in master.Comp.Pets)
        {
            // Получаем компонент питомца, чтобы достать оттуда список доступных приказов
            if (!TryComp<PettableOnInteractComponent>(pet, out var petComponent))
                continue;

            // Если питомец не имеет приказа атаковать, то он пропускается
            if (!petComponent.AllowedOrders.Contains(AttackOrder))
                continue;

            // Вызываем ивент, информирующий о смене логики поведения питомца на приказ атаки, передавая цель.
            RaiseLocalEvent(pet, new PetSetAILogicEvent(AttackOrder, args.Target));
        }
    }

    #endregion

    #region Pets compability

    /// <summary>
    /// Метод, работающий с логикой передачи всех питомцев от одного хозяина к другому.
    /// Если нового хозяина нет, метод очищает весь список питомцев у старого владельца и отвязывает их.
    /// </summary>
    /// <param name="master">Entity старого хозяина</param>
    /// <param name="newMaster">EntityUid нового хозяина, если есть. В него перейдут все питомцы и добавятся все нужные для этого акшены</param>
    private void CleanMaster(Entity<PetOnInteractComponent> master, EntityUid? newMaster = null)
    {
        // Захешировал список, так как хз, как поведет себя цикл при удалении оттуда питомцев при итерации
        var oldPets = master.Comp.Pets;

        // Проходимся по списку всех питомцев у старого владельца
        foreach (var petUid in oldPets)
        {
            if (!TryComp<PettableOnInteractComponent>(petUid, out var petComponent))
                continue;

            var petEntity = (petUid, petComponent);

            // Убираем питомца из списка питомцев старого тела
            RemoveMaster(petEntity, master);

            // Если хозяин null, значит мы не передаем питомцев в новое тело.
            // Потоум что тела или нет, или это нам не требуется
            if (!newMaster.HasValue)
                continue;

            if (!TryComp<PetOnInteractComponent>(newMaster, out var newMasterComponent))
                continue;

            // Новое тела хозяина
            var newMasterEntity = (newMaster.Value, newMasterComponent);

            // Добавляем питомца в нового мастера
            TrySetMaster(petEntity, newMasterEntity, true);

            // Задаем питомцу правильную логику и добавляем владельцу акшены
            Pet(petEntity, true);
        }

        // TODO: В идеале список питомцев должен храниться в Mind`е хозяина, чтобы автоматически переноситься в новое тело, как только туда зайдет игрок
    }

    /// <summary>
    /// Метод, обрабатывающий логику передачи питомцев от старого тела хозяина к новому при клонировании
    /// </summary>
    /// <param name="master">Entity прошлого тела хозяина</param>
    /// <param name="args">Ивент типа CloningEvent</param>
    private void OnMasterCloned(Entity<PetOnInteractComponent> master, ref CloningEvent args)
    {
        CleanMaster(master, args.CloneUid);
    }

    /// <summary>
    /// Метод, обрабатывающий логику отвязывания питомцев от хозяина, когда его тело гибнули.
    /// </summary>
    /// <param name="master">Entity хозяина</param>
    /// <param name="args">Ивент типа ComponentShutdown</param>
    private void OnMasterShutdown(Entity<PetOnInteractComponent> master, ref ComponentShutdown args)
    {
        CleanMaster(master);
    }

    /// <summary>
    /// Метод, занимающийся обработкой последствий после компонента питомца или его целиком из реальности.
    /// </summary>
    /// <param name="pet">Entity питомца</param>
    /// <param name="args">Ивент типа ComponentShutdown</param>
    private void OnPetShutdown(Entity<PettableOnInteractComponent> pet, ref ComponentShutdown args)
    {
        var master = pet.Comp.Master;

        if (!master.HasValue)
            return;

        if (!TryComp<PetOnInteractComponent>(master, out var masterComponent))
            return;

        var masterEntity = (master.Value, masterComponent);

        MasterForgetPet(masterEntity, pet);
    }

    #endregion
}

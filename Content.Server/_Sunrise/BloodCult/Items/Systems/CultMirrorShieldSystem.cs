using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Sunrise.BloodCult.Items.Components;
using Content.Server.Body.Components;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.NPC.Components;
using Content.Server.Station.Systems;
using Content.Server.Stunnable;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Items;
using Content.Shared._Sunrise.Events;
using Content.Shared.Blocking;
using Content.Shared.Body.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.SSDIndicator;
using Content.Shared.Strip.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server._Sunrise.BloodCult.Items.Systems;

/// <summary>
///     Система для обработки логики зеркального щита. Референс: https://youtu.be/SiFY7ek_91Y?t=330&si=GB2jxaBrhe2vG5vc
/// </summary>
public sealed partial class CultMirrorShieldSystem : EntitySystem
{
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StunSystem _stun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private StationSystem _stations = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private ISharedPlayerManager _playerMan = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;

    private static readonly ProtoId<NpcFactionPrototype> BloodCultFaction = "BloodCult";
    private static readonly ProtoId<NpcFactionPrototype> PassiveFaction = "Passive";

    private ISawmill _sawmill = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        // Initialize subsystems
        InitializeCommands();

        _sawmill = _log.GetSawmill("mirrorshield");

        SubscribeLocalEvent<CultMirrorShieldComponent, BlockingEvent>(OnMeleeBlocking);
        SubscribeLocalEvent<CultMirrorShieldComponent, ReflectedEvent>(OnHitScanReflected);
        SubscribeLocalEvent<CultMirrorShieldComponent, ComponentShutdown>(OnShieldShutdown);

        SubscribeLocalEvent<CultMirrorIllusionComponent, ComponentInit>(OnIllusionInit);
        SubscribeLocalEvent<CultMirrorIllusionComponent, MobStateChangedEvent>(OnIllusionMobStateChanged);
        SubscribeLocalEvent<CultMirrorIllusionComponent, BeingGibbedEvent>(OnIllusionGib);
        SubscribeLocalEvent<CultMirrorIllusionComponent, ComponentShutdown>(OnIllusionShutdown);
    }

    private void OnMeleeBlocking(EntityUid uid, CultMirrorShieldComponent component, BlockingEvent args)
    {
        if (TryBreakShield(uid, args.Damage))
            return;
        TrySpawnIllusion(uid);
    }

    #region ShieldBreak

    /// <summary>
    /// Щит отразил что-то
    /// </summary>
    private void OnHitScanReflected(EntityUid uid, CultMirrorShieldComponent component, ReflectedEvent args)
    {
        if (args.Damage == null)
            return;
        if (TryBreakShield(uid, args.Damage))
            return;
        TrySpawnIllusion(uid);
    }

    /// <summary>
    ///     Логика ломания щита
    /// </summary>
    public bool TryBreakShield(Entity<CultMirrorShieldComponent?> entity, DamageSpecifier damage)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return false;

        var xform = Transform(entity);
        var owner = xform.ParentUid;
        if (!HasComp<BloodCultistComponent>(owner))
            return false;

        var sum = CalculateDamage(damage);
        _sawmill.Debug($"sum damage: {sum}");

        var chance = CalculateChance(sum);

        if (_random.NextFloat() > chance)
            return false;

        BreakShield(entity);

        return true;
    }

    /// <summary>
    ///     Само ломание щита
    /// </summary>
    public void BreakShield(Entity<CultMirrorShieldComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return;

        // Стан хозяина
        var xform = Transform(entity);
        var parent = xform.ParentUid;
        _stun.TryKnockdown(parent, entity.Comp.KnockdownDuration, true);

        // Звук ломания
        _audio.PlayPvs(entity.Comp.BreakSound, parent);

        // Попап
        _popup.PopupEntity(Loc.GetString("cultshield-broken", ("name", MetaData(entity.Owner).EntityName)),
            parent,
            PopupType.LargeCaution);

        // Удаление щита
        QueueDel(entity.Owner);
    }

    #endregion ShieldBreak

    #region Illusion

    /// <summary>
    /// Иллюзии должны разбиваться после смерти
    /// </summary>
    private void OnIllusionMobStateChanged(EntityUid uid, CultMirrorIllusionComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;
        QueueDel(uid);
    }

    /// <summary>
    /// Иллюзии должны быть дохлыми
    /// </summary>
    private void OnIllusionInit(EntityUid uid, CultMirrorIllusionComponent component, ComponentInit args)
    {
        EnsureComp<MobStateComponent>(uid);
        EnsureComp<MobThresholdsComponent>(uid);
        EnsureComp<DamageableComponent>(uid);

        _mobThreshold.SetMobStateThreshold(uid, 15, MobState.Critical);
        _mobThreshold.SetMobStateThreshold(uid, 20, MobState.Dead);
    }

    /// <summary>
    /// Иллюзию разбили. Вычеркиваем
    /// </summary>
    private void OnIllusionShutdown(EntityUid uid, CultMirrorIllusionComponent component, ComponentShutdown args)
    {
        if (!TryComp<CultMirrorShieldComponent>(component.ParentShield, out var mirror))
            return;
        mirror.Illusions.Remove(uid);
    }

    /// <summary>
    /// А что если иллюзию гибнули быстрее, чем она успела умереть? Крайний случай, эта функция не должна быть вызвана никогда
    /// </summary>
    private void OnIllusionGib(EntityUid uid, CultMirrorIllusionComponent component, BeingGibbedEvent args)
    {
        if (!TryComp<CultMirrorShieldComponent>(component.ParentShield, out var mirror))
            return;
        mirror.Illusions.Remove(uid);
    }

    /// <summary>
    /// Щит разбился. Иллюзии должны разбиться вместе с ним
    /// </summary>
    private void OnShieldShutdown(EntityUid uid, CultMirrorShieldComponent component, ComponentShutdown args)
    {
        foreach (var illusion in component.Illusions)
        {
            QueueDel(illusion);
        }
    }

    /// <summary>
    ///     Логика спавна иллюзий
    /// </summary>
    public void TrySpawnIllusion(Entity<CultMirrorShieldComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return;

        if (_random.NextFloat() > entity.Comp.IllusionChance)
            return;

        var xform = Transform(entity);
        var owner = xform.ParentUid;
        EntityUid? illusion = null;
        if (HasComp<BloodCultistComponent>(owner))
        {
            // Количество иллюзий ограничено только если хозяин щита культист
            if (entity.Comp.Illusions.Count > entity.Comp.MaxIllusionCounter)
                return;

            // Если хозяин культист, то...
            if (_random.NextFloat() < 0.8f)
                CreateIllusion(owner, out illusion, agressive: true);
            else
                CreateIllusion(owner, out illusion, agressive: false);
        }
        else
        {
            // Если хозяин не культист, то...
            CreateIllusion(owner, out illusion, false, owner);
        }

        if (illusion == null)
            return;

        var illusionComp = EnsureComp<CultMirrorIllusionComponent>(illusion.Value);
        illusionComp.ParentShield = entity.Owner;

        entity.Comp.Illusions.Add(illusion.Value);
    }

    public bool CreateIllusion(EntityUid uid, [NotNullWhen(true)] out EntityUid? illusionUid, bool agressive = true, EntityUid? targetAggro = null)
    {
        illusionUid = null;

        if (!CreateBaseIllusion(uid, out var mobUid, agressive, targetAggro))
            return false;
        if (!CloneGear(uid, mobUid.Value))
            return false;

        illusionUid = mobUid;

        return true;
    }

    #endregion Illusion

    # region Helper functions

    public bool CloneGear(EntityUid uid, EntityUid mobUid)
    {
        if (!_inventory.TryGetContainerSlotEnumerator(uid, out var originSlotEnumerator))
            return false;
        while (originSlotEnumerator.MoveNext(out var containerSlot))
        {
            if (containerSlot.ContainedEntity is null)
                continue;
            if (!CloneItem(containerSlot.ContainedEntity.Value, out var item))
                continue;
            EnsureComp<UnremoveableComponent>(item.Value);
            _inventory.TryEquip(mobUid, item.Value, containerSlot.ID, true, true);
        }

        List<EntityUid> heldItems = new();
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (!CloneItem(held, out var item))
                continue;
            EnsureComp<UnremoveableComponent>(item.Value);
            heldItems.Add(item.Value);
        }

        if (heldItems.Count != 0)
        {
            foreach (var hand in _hands.EnumerateHands(mobUid).Reverse())
            {
                if (heldItems.Count == 0 || !HasComp<CultMirrorShieldComponent>(heldItems[0]))
                {
                    _hands.TrySetActiveHand(mobUid, hand);
                }
                if (heldItems.Count != 0)
                {
                    _hands.TryPickup(mobUid, heldItems[0], hand);
                    heldItems.RemoveAt(0);
                }
            }
        }

        // А что если у цели было больше двух рук? У клона же в свою очередь будет скорее всего 2
        foreach (var leftoveritem in heldItems)
        {
            QueueDel(leftoveritem);
        }

        return true;
    }

    public bool CreateBaseIllusion(EntityUid uid, [NotNullWhen(true)] out EntityUid? mobUid, bool wasSourceCultist = true, EntityUid? userUid = null, bool agressive = true)
    {
        mobUid = null;
        if (!_transform.TryGetMapOrGridCoordinates(uid, out var coords))
        {
            return false;
        }

        var stationUid = _stations.GetOwningStation(uid);

        if (!_playerMan.TryGetSessionByEntity(uid, out var session))
            return false;

        var profile = _ticker.GetPlayerProfile(session);
        mobUid = _spawning.SpawnPlayerMob(coords.Value, null, profile, stationUid);

        if (HasComp<SSDIndicatorComponent>(mobUid))
            RemComp<SSDIndicatorComponent>(mobUid.Value);
        if (HasComp<StrippableComponent>(mobUid))
            RemComp<StrippableComponent>(mobUid.Value);

        // Через 15 секунд иллюзия удаляется - Не канон, удали эту хуету
        var timedDelete = EnsureComp<TimedDespawnComponent>(mobUid.Value);
        timedDelete.Lifetime = wasSourceCultist ? 10f : 15f;

        _faction.ClearFactions(mobUid.Value);
        if (wasSourceCultist)
        {
            // Она должна атаковать всех не культистов
            if (agressive)
            {
                _faction.AddFaction(mobUid.Value, BloodCultFaction);
                _console.ExecuteCommand($"addnpc {mobUid.Value} HostileIllusionCompound");
            }
            else
            {
                _faction.AddFaction(mobUid.Value, PassiveFaction);
                EnsureComp<NPCRetaliationComponent>(mobUid.Value);
                _console.ExecuteCommand($"addnpc {mobUid.Value} IdleCompound");
            }
        }
        else
        {
            if (userUid != null)
            {
                _faction.AggroEntity(mobUid.Value, userUid.Value);
                _console.ExecuteCommand($"addnpc {mobUid.Value} HostileIllusionCompound");
            }
        }

        return true;
    }

    public bool CloneItem(EntityUid origin, [NotNullWhen(true)] out EntityUid? item)
    {
        item = null;
        var meta = MetaData(origin);
        var proto = meta.EntityPrototype;
        if (proto is null)
            return false;
        item = Spawn(proto.ID);
        return true;
    }

    public FixedPoint2 CalculateDamage(DamageSpecifier damageSpecifier)
    {
        FixedPoint2 sum = 0;
        foreach (var damageDictKey in damageSpecifier.DamageDict.Keys)
        {
            damageSpecifier.DamageDict.TryGetValue(damageDictKey, out var damage);
            sum += damage;
        }

        return sum;
    }

    public FixedPoint2 CalculateChance(FixedPoint2 sum)
    {
        return Math.Clamp((sum.Float() / 100f - 0.1f) * 3f, 0f, 0.75f);
    }

    # endregion Helper functions
}

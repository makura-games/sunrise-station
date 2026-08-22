using Content.Server.Popups;
using Content.Shared._Starlight.Weapon.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared._Sunrise.Execution;
using Content.Shared.Body.Components;
using Content.Shared.Kitchen.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Body.Systems;
using Content.Shared.ActionBlocker;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Execution;

public sealed partial class ExecutionSystem : SharedExecutionSystem
{
    [Dependency] private ContainerSystem _containerSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private AppearanceSystem _appearanceSystem = default!;
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private ExplosionSystem _explosionSystem = default!;
    [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private SharedGunSystem _gunSystem = default!;

    private const float OverkillFractionMin = 0.05f;
    private const float OverkillFractionMax = 0.20f;
    private const float ExplosiveOverkillFractionMin = 2.0f;
    private const float ExplosiveOverkillFractionMax = 4.0f;
    private const float SuicideExplosionIntensityScale = 0.45f;
    private const int SuicideExplosionMaxTileBreak = 0;
    private const bool SuicideExplosionCanCreateVacuum = false;

    private readonly record struct SuicideExplosionInfo(
        ProtoId<ExplosionPrototype> ExplosionType,
        float TotalIntensity,
        float IntensitySlope,
        float MaxIntensity,
        float TileBreakScale);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharpComponent, ExecutionDoAfterEvent>(OnDoafterMelee);
        SubscribeLocalEvent<GunComponent, ExecutionDoAfterEvent>(OnDoafterGun);
    }

    private void OnDoafterMelee(Entity<SharpComponent> ent, ref ExecutionDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used == null || args.Target == null)
            return;

        var attacker = args.User;
        var victim = args.Target!.Value;
        var weapon = args.Used!.Value;

        if (!CanExecuteWithMeleeServer(weapon, victim, attacker))
            return;

        if (!TryComp<MeleeWeaponComponent>(weapon, out var melee))
            return;

        ApplyExecutionDamage(victim, weapon, melee.Damage, forceLethal: true, OverkillFractionMin, OverkillFractionMax);
        _audioSystem.PlayEntity(melee.HitSound, Filter.Pvs(weapon), weapon, true, AudioParams.Default);

        var internalKey = attacker == victim
            ? "suicide-popup-melee-complete-internal"
            : "execution-popup-melee-complete-internal";

        var externalKey = attacker == victim
            ? "suicide-popup-melee-complete-external"
            : "execution-popup-melee-complete-external";

        ShowExecutionPopup(internalKey, Filter.Entities(attacker), PopupType.Medium, attacker, victim, weapon);
        ShowExecutionPopup(externalKey, Filter.PvsExcept(attacker), PopupType.MediumCaution, attacker, victim, weapon);
    }

    // TODO: This repeats a lot of the code of the serverside GunSystem, make it not do that
    private void OnDoafterGun(Entity<GunComponent> ent, ref ExecutionDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used == null || args.Target == null)
            return;

        var attacker = args.User;
        var weapon = args.Used.Value;
        var victim = args.Target.Value;

        if (!CanExecuteWithGunServer(weapon, victim, attacker))
            return;

        var prevention = new ShotAttemptedEvent
        {
            User = attacker,
            Used = (weapon, ent.Comp),
        };

        RaiseLocalEvent(weapon, ref prevention);
        if (prevention.Cancelled)
            return;

        RaiseLocalEvent(attacker, ref prevention);
        if (prevention.Cancelled)
            return;

        var attemptEv = new AttemptShootEvent(attacker, null);
        RaiseLocalEvent(weapon, ref attemptEv);

        if (attemptEv.Cancelled)
        {
            if (attemptEv.Message != null)
                _popupSystem.PopupClient(attemptEv.Message, weapon, attacker);
            return;
        }

        var fromCoordinates = Transform(attacker).Coordinates;
        var takeAmmoEvent = new TakeAmmoEvent(
            GunExecutionShots,
            new List<(EntityUid? Entity, IShootable Shootable)>(),
            fromCoordinates,
            attacker);
        RaiseLocalEvent(weapon, takeAmmoEvent);

        if (takeAmmoEvent.Ammo.Count <= 0)
        {
            _audioSystem.PlayEntity(ent.Comp.SoundEmpty, Filter.Pvs(weapon), weapon, true, AudioParams.Default);
            ShowExecutionPopup("execution-popup-gun-empty", Filter.Pvs(weapon), PopupType.Medium, attacker, victim, weapon);
            return;
        }

        DamageSpecifier damage = new();
        SuicideExplosionInfo? explosiveToTrigger = null;
        string? firedPrototypeId = null;

        var ammoUid = takeAmmoEvent.Ammo[0].Entity;
        var shootable = takeAmmoEvent.Ammo[0].Shootable;
        var isSpent = shootable switch
        {
            CartridgeAmmoComponent x => x.Spent,
            HitScanCartridgeAmmoComponent x => x.Spent,
            _ => false
        };

        if (isSpent)
        {
            if (ammoUid != null)
            {
                if (_containerSystem.TryGetContainer(weapon, GunChamberContainerId, out var chamberContainer))
                    _containerSystem.Remove(ammoUid.Value, chamberContainer);

                _transformSystem.DropNextTo(ammoUid.Value, weapon);
            }

            _audioSystem.PlayEntity(ent.Comp.SoundEmpty, Filter.Pvs(weapon), weapon, true, AudioParams.Default);
            ShowExecutionPopup("execution-popup-ammo-empty", Filter.Pvs(weapon), PopupType.Medium, attacker, victim, weapon);
            return;
        }

        switch (takeAmmoEvent.Ammo[0].Shootable)
        {
            //🌟Starlight🌟 start
            case HitScanCartridgeAmmoComponent cartridge:
                var hitscanProto = _prototypeManager.Index(cartridge.Hitscan);
                firedPrototypeId = cartridge.Hitscan.Id;

                if (hitscanProto.Damage is not null)
                    damage = hitscanProto.Damage * hitscanProto.Count;

                cartridge.Spent = true;
                _appearanceSystem.SetData(ammoUid!.Value, AmmoVisuals.Spent, true);

                Dirty(ammoUid.Value, cartridge);

                if (cartridge.DeleteOnSpawn)
                    Del(ammoUid.Value);

                break;
            //🌟Starlight🌟 end
            case CartridgeAmmoComponent cartridge:
                var prototype = _prototypeManager.Index<EntityPrototype>(cartridge.Prototype);
                firedPrototypeId = cartridge.Prototype.Id;
                prototype.TryGetComponent<ProjectileComponent>(out var projectilePrototype, _componentFactory);

                if (projectilePrototype != null)
                    damage = projectilePrototype.Damage;
                else if (prototype.TryGetComponent<HitscanBasicDamageComponent>(out var hitscanDamage, _componentFactory) && hitscanDamage != null)
                    damage = hitscanDamage.Damage;

                if (prototype.TryGetComponent<ExplosiveComponent>(out var explosiveProto, _componentFactory) && explosiveProto != null)
                {
                    explosiveToTrigger = new SuicideExplosionInfo(
                        explosiveProto.ExplosionType,
                        explosiveProto.TotalIntensity,
                        explosiveProto.IntensitySlope,
                        explosiveProto.MaxIntensity,
                        explosiveProto.TileBreakScale);
                }

                prototype.TryGetComponent<ProjectileSpreadComponent>(out var projectileSpread, _componentFactory);

                if (projectileSpread != null)
                    damage *= projectileSpread.Count;

                cartridge.Spent = true;
                _appearanceSystem.SetData(ammoUid!.Value, AmmoVisuals.Spent, true);
                Dirty(ammoUid.Value, cartridge);

                if (cartridge.DeleteOnSpawn)
                    Del(ammoUid.Value);

                break;

            case AmmoComponent:
                if (ammoUid != null)
                    firedPrototypeId = MetaData(ammoUid.Value).EntityPrototype?.ID;

                TryComp<ProjectileComponent>(ammoUid, out var projectileAmmo);

                if (projectileAmmo != null)
                    damage = projectileAmmo.Damage;
                else if (ammoUid != null && TryComp<HitscanBasicDamageComponent>(ammoUid, out var hitscanAmmoDamage) && !hitscanAmmoDamage.Damage.Empty)
                    damage = hitscanAmmoDamage.Damage;

                if (ammoUid != null && TryComp<ExplosiveComponent>(ammoUid.Value, out var explosiveAmmo))
                {
                    explosiveToTrigger = new SuicideExplosionInfo(
                        explosiveAmmo.ExplosionType,
                        explosiveAmmo.TotalIntensity,
                        explosiveAmmo.IntensitySlope,
                        explosiveAmmo.MaxIntensity,
                        explosiveAmmo.TileBreakScale);
                }

                if (ammoUid != null)
                    Del(ammoUid.Value);

                break;

            case HitscanAmmoComponent:
                if (ammoUid != null)
                    firedPrototypeId = MetaData(ammoUid.Value).EntityPrototype?.ID;

                if (ammoUid != null && TryComp<HitscanBasicDamageComponent>(ammoUid, out var hitscanAmmo) && !hitscanAmmo.Damage.Empty)
                    damage = hitscanAmmo.Damage;

                if (ammoUid != null)
                    Del(ammoUid.Value);

                break;

            case HitscanPrototype hitscan:
                firedPrototypeId = hitscan.ID;
                damage = hitscan.Damage!;
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        var forceLethal = !IsNonLethalAmmoServer(firedPrototypeId);
        var isExplosive = explosiveToTrigger != null;

        if (isExplosive && forceLethal)
        {
            var explosionType = _prototypeManager.Index(explosiveToTrigger!.Value.ExplosionType);
            ApplyExecutionDamage(victim, weapon, explosionType.DamagePerIntensity, forceLethal: true, ExplosiveOverkillFractionMin, ExplosiveOverkillFractionMax);

            if (TryComp<BloodstreamComponent>(victim, out var bloodstream))
                _bloodstreamSystem.SpillAllSolutions((victim, bloodstream));
        }

        else
            ApplyExecutionDamage(victim, weapon, damage, forceLethal, OverkillFractionMin, OverkillFractionMax);

        if (explosiveToTrigger != null)
        {
            var explosive = explosiveToTrigger.Value;
            _explosionSystem.QueueExplosion(
                victim,
                explosive.ExplosionType,
                explosive.TotalIntensity * SuicideExplosionIntensityScale,
                explosive.IntensitySlope,
                explosive.MaxIntensity * SuicideExplosionIntensityScale,
                explosive.TileBreakScale,
                SuicideExplosionMaxTileBreak,
                SuicideExplosionCanCreateVacuum,
                attacker);
        }

        _audioSystem.PlayEntity(ent.Comp.SoundGunshotModified ?? ent.Comp.SoundGunshot, Filter.Pvs(weapon), weapon, false, AudioParams.Default);

        var internalKey = attacker != victim
            ? "execution-popup-gun-complete-internal"
            : "suicide-popup-gun-complete-internal";

        var externalKey = attacker != victim
            ? "execution-popup-gun-complete-external"
            : "suicide-popup-gun-complete-external";

        ShowExecutionPopup(internalKey, Filter.Entities(attacker), PopupType.LargeCaution, attacker, victim, weapon);
        ShowExecutionPopup(externalKey, Filter.PvsExcept(attacker), PopupType.LargeCaution, attacker, victim, weapon);
    }
}

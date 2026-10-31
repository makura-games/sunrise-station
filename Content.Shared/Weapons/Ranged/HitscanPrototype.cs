using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Physics;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Starlight.Utility;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;
using Content.Shared._Sunrise.Weapons.Enums;

namespace Content.Shared.Weapons.Ranged;

[Prototype]
public sealed partial class HitscanPrototype : IPrototype, IShootable, IInheritingPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name")]
    public string Name { get; private set; } = string.Empty;

    [ViewVariables(VVAccess.ReadWrite), DataField("walkSpeedMultiplier")]
    public float WalkSpeedMultiplier = 1f;

    [ViewVariables(VVAccess.ReadWrite), DataField("runSpeedMultiplier")]
    public float RunSpeedMultiplier = 1f;

    [ViewVariables(VVAccess.ReadWrite), DataField("damage")]
    public DamageSpecifier? Damage;

    [ViewVariables(VVAccess.ReadOnly), DataField("muzzleFlash")]
    public SpriteSpecifier? MuzzleFlash;

    [ViewVariables(VVAccess.ReadOnly), DataField("travelFlash")]
    public SpriteSpecifier? TravelFlash;

    [ViewVariables(VVAccess.ReadOnly), DataField("impactFlash")]
    public SpriteSpecifier? ImpactFlash;

    [ViewVariables(VVAccess.ReadOnly), DataField("bullet")]
    public ExtendedSpriteSpecifier? Bullet;

    [DataField("collisionMask")]
    public int CollisionMask = (int) CollisionGroup.Opaque;

    /// <summary>
    /// What we count as for reflection.
    /// </summary>
    [DataField("reflective")] public ReflectType Reflective = ReflectType.Energy;

    /// <summary>
    /// Sound that plays upon the thing being hit.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Force the hitscan sound to play rather than potentially playing the entity's sound.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("forceSound")]
    public bool ForceSound;

    /// <summary>
    /// Try not to set this too high.
    /// </summary>
    [DataField("maxLength")]
    public float MaxLength = 20f;

    /// <summary>
    /// How much the ammo spreads when shot, in degrees. Does nothing if count is 0.
    /// </summary>
    [DataField]
    public Angle Spread = Angle.FromDegrees(5);

    [DataField]
    public int Count = 1;

    // Sunrise-Start
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<HitscanPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [ViewVariables(VVAccess.ReadWrite), DataField("staminaDamage")]
    public float StaminaDamage;

    [ViewVariables(VVAccess.ReadWrite), DataField("knockdownAmount")]
    public float KnockdownAmount;

    [ViewVariables(VVAccess.ReadWrite), DataField("stunAmount")]
    public float StunAmount;

    [ViewVariables(VVAccess.ReadWrite), DataField("emp")]
    public EmpProperties? Emp;

    [ViewVariables(VVAccess.ReadWrite), DataField("slowdownAmount")]
    public float SlowdownAmount;

    [ViewVariables(VVAccess.ReadWrite), DataField("ricochetChance")]
    public float RicochetChance = 0f;

    [ViewVariables(VVAccess.ReadWrite), DataField("pierceChance")]
    public float PierceChance = 0.10f;

    [ViewVariables(VVAccess.ReadWrite), DataField("derivation")]
    public float Derivation = 0.10f;

    [ViewVariables(VVAccess.ReadWrite), DataField("steps")]
    public int Steps = 5;

    [ViewVariables(VVAccess.ReadWrite), DataField("pierceLevel")]
    public PierceLevel PierceLevel = PierceLevel.Flesh;

    [ViewVariables(VVAccess.ReadWrite), DataField("armorPenetration")]
    public float ArmorPenetration = 0f;

    [ViewVariables(VVAccess.ReadWrite), DataField("speed")]
    public float Speed = 315f;

    [DataField("igniteOnCollision"), ViewVariables(VVAccess.ReadWrite)]
    public bool Ignite = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IgnoreResistances = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Temperature = 1050f;
    // Sunrise-End
}

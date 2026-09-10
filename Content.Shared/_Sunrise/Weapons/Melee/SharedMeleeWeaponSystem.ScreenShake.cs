using Content.Shared._Sunrise.Camera;
using Content.Shared.Damage;
using Content.Shared.Wieldable.Components;

// Файл намеренно расположен в _Sunrise, но расширяет ванильный partial-класс.
#pragma warning disable IDE0130
namespace Content.Shared.Weapons.Melee;

public abstract partial class SharedMeleeWeaponSystem
{
    [Dependency] private readonly SunriseScreenShakeSystem _sunriseScreenShake = default!;

    private void AddSunriseMeleeScreenShake(
        EntityUid weapon,
        DamageSpecifier damage,
        EntityUid attacker,
        List<EntityUid> targets)
    {
        if (damage.GetTotal() > 8)
        {
            var targetTranslation = new SunriseScreenShakeParameters
            {
                Trauma = 0.45f,
                DecayRate = 1.1f,
                Frequency = 0.04f,
            };

            foreach (var target in targets)
                _sunriseScreenShake.Shake(target, targetTranslation, null);
        }

        var strongBluntHit = damage.DamageDict.TryGetValue("Blunt", out var blunt) && blunt >= 20;
        var wieldedWeapon = TryComp<WieldableComponent>(weapon, out var wieldable) && wieldable.Wielded;
        if (!strongBluntHit && !wieldedWeapon)
            return;

        var attackerRotation = new SunriseScreenShakeParameters
        {
            Trauma = 0.08f,
            DecayRate = 1f,
            Frequency = 0.009f,
        };
        _sunriseScreenShake.Shake(attacker, null, attackerRotation);
    }
}

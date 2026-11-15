#if SUNRISE_PRIVATE
using Content.Shared.Melonity;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Melee;

public abstract partial class SharedMeleeWeaponSystem
{
    /// <summary>
    ///     Приватная проверка запросов. Сами проверки лежат в SunrisePrivate, здесь только зависимость,
    ///     чтобы не менять поля в файле, который обновляется из апстрима.
    /// </summary>
    [Dependency] private SharedAntiCheatSystem _sunriseAntiCheat = default!;
}
#endif

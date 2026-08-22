using System.Linq;
using Content.Shared.Random.Helpers;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Random;

/// <summary>
/// Система для генерации предсказуемых случайных чисел.
/// Позволяет получать синхронизированные псевдорандомные числа между сервером и клиентом.
/// </summary>
/// <remarks>
/// Учтите, что результат БУДЕТ не похож на случайные числа из-за использования простой математической формулы для получения сида.
/// </remarks>
/// <seealso cref="SharedRandomExtensions"/>
public sealed partial class RandomPredictedSystem : EntitySystem
{
    /*
     * Основная часть системы.
     */

    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeTickBased();
    }

    /// <summary>
    /// Создает или получает экземпляр рандома для конкретных сущностей.
    /// Сид зависит от текущего тика и ID сущностей, что обеспечивает предсказуемость.
    /// </summary>
    /// <param name="entities">Список <see cref="EntityUid"/> на основе ID которого будет основываться сид для рандома.</param>
    /// <remarks>
    /// Чем больше разных сущностей будет использоваться, тем случайнее будет выдаваемый результат.
    /// </remarks>
    private System.Random GetOrCreateEntityRandom(List<EntityUid> entities)
    {
        var list = entities
            .Select(e => GetNetEntity(e).Id)
            .ToList();

        list.Add((int)_timing.CurTick.Value);

        var seed = SharedRandomExtensions.HashCodeCombine(list);
        var random = new System.Random(seed);

        return random;
    }

    /// <summary>
    /// Создает или получает экземпляр рандома для конкретной сущности.
    /// Сид зависит от текущего тика и ID сущности, что обеспечивает предсказуемость.
    /// </summary>
    /// <param name="uid"><see cref="EntityUid"/> на основе ID которого будет основываться сид для рандома.</param>
    private System.Random GetOrCreateEntityRandom(EntityUid uid)
    {
        var ent = GetNetEntity(uid);
        var seed = SharedRandomExtensions.HashCodeCombine(new List<int> { (int) _timing.CurTick.Value, ent.Id });
        var random = new System.Random(seed);

        return random;
    }
}


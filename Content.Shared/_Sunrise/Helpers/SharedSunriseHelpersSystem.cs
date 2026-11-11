using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Shared._Sunrise.Helpers;

public abstract partial class SharedSunriseHelpersSystem : EntitySystem
{
    [Dependency] private EntityManager _entMan = default!;

    #region Get All/First entity

    /// <summary>
    /// Получает все список всех ентити с компонентами и возвращает.
    /// Удобно для использования, так как не требует засорять код лишним циклом
    /// </summary>
    /// <typeparam name="T1">Компонент 1</typeparam>
    /// <typeparam name="T2">Компонент 2</typeparam>
    /// <remarks>Список может быть пустым, если ничего не найдено</remarks>
    /// <returns>Полный список всех ентити в игре с данными компонентами</returns>
    public IEnumerable<Entity<T1, T2>> GetAll<T1, T2>() where T1 : IComponent where T2 : IComponent
    {
        var query = _entMan.AllEntityQueryEnumerator<T1, T2>();
        while (query.MoveNext(out var uid, out var component1, out var component2))
        {
            yield return (uid, component1, component2);
        }
    }

    /// <summary>
    /// Получает все список всех ентити с компонентом и возвращает.
    /// Удобно для использования, так как не требует засорять код лишним циклом
    /// </summary>
    /// <typeparam name="T">Компонент</typeparam>
    /// <remarks>Список может быть пустым, если ничего не найдено</remarks>
    /// <returns>Полный список всех ентити в игре с данным компонентом БЕЗ учета сущности в состоянии паузы</returns>
    public IEnumerable<Entity<T>> GetAll<T>() where T : IComponent
    {
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var component))
        {
            yield return (uid, component);
        }
    }

    /// <summary>
    /// Получает все список всех ентити с компонентом и возвращает.
    /// Удобно для использования, так как не требует засорять код лишним циклом
    /// </summary>
    /// <typeparam name="T">Компонент</typeparam>
    /// <remarks>Список может быть пустым, если ничего не найдено</remarks>
    /// <returns>Полный список всех ентити в игре с данным компонентом С УЧЕТОМ сущностей в состоянии паузы</returns>
    public IEnumerable<Entity<T>> GetAllWithPaused<T>() where T : IComponent
    {
        var query = AllEntityQuery<T>();
        while (query.MoveNext(out var uid, out var component))
        {
            yield return (uid, component);
        }
    }

    /// <summary>
    /// Возвращает первый попавшийся ентити с данным компонентом
    /// </summary>
    /// <param name="entity">Возвращаемый ентити</param>
    /// <typeparam name="T">Компонент</typeparam>
    /// <returns>Первый попавшийся ентити с данным компонентом</returns>
    public bool TryGetFirst<T>([NotNullWhen(true)] out Entity<T>? entity) where T : IComponent
    {
        entity = null;

        var query = _entMan.AllEntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var component))
        {
            entity = (uid, component);
            return true;
        }

        return false;
    }

    #endregion
}

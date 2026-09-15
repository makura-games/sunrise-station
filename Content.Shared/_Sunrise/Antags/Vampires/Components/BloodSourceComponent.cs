using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Стадия гниения: порог времени после смерти и коэффициент эффективности крови.
/// Аналог StunComponent — стадии задаются границами времени.
/// </summary>
[DataDefinition]
public sealed partial class RotEfficiencyStage
{
    /// <summary>
    /// Время с момента смерти, с которого действует этот коэффициент.
    /// Список должен быть отсортирован по возрастанию; последний элемент обычно TimeSpan.MaxValue.
    /// </summary>
    [DataField]
    public TimeSpan TimeAfterDeath;

    /// <summary>
    /// Коэффициент эффективности крови на данной стадии гниения (0.0 - 1.0).
    /// </summary>
    [DataField]
    public float Efficiency = 1.0f;
}

/// <summary>
/// Определяет эффективность крови цели для питья. Вешается НА ЦЕЛЬ.
/// Коэффициенты (человек/животное/мёртвое/гниение) определяются состоянием цели,
/// а не жёсткими значениями в компоненте вампира.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodSourceComponent : Component
{
    /// <summary>
    /// Базовая эффективность живой цели (человек = 0.5, животное = 0.125 и т.д.).
    /// </summary>
    [DataField]
    public float BaseEfficiency = 0.5f;

    /// <summary>
    /// Множитель эффективности для мёртвой цели (0 отключает питьё из мёртвых).
    /// </summary>
    [DataField]
    public float DeadEfficiency = 0.75f;

    /// <summary>
    /// Стадии гниения цели: порог времени после смерти и коэффициент.
    /// Применяется только к мёртвым/гниющим целям.
    /// </summary>
    [DataField]
    public List<RotEfficiencyStage> RotStages = new()
    {
        // свежее (< 30с)
        new() { TimeAfterDeath = TimeSpan.FromSeconds(0), Efficiency = 1.0f },
        // начальная стадия (< 3:30)
        new() { TimeAfterDeath = TimeSpan.FromSeconds(30), Efficiency = 0.5f },
        // средняя стадия (< 6:45)
        new() { TimeAfterDeath = TimeSpan.FromSeconds(210), Efficiency = 0.25f },
        // поздняя стадия (< 10:00)
        new() { TimeAfterDeath = TimeSpan.FromSeconds(405), Efficiency = 0.1f },
        // полное гниение (>= 10:00)
        new() { TimeAfterDeath = TimeSpan.FromSeconds(600), Efficiency = 0.0f },
    };

    /// <summary>
    /// Возвращает коэффициент эффективности для заданного времени гниения.
    /// Берёт последнюю стадию, чей порог времени меньше или равен переданному.
    /// </summary>
    public float GetRotEfficiency(TimeSpan rotTime)
    {
        var efficiency = 1.0f;
        foreach (var stage in RotStages)
        {
            if (rotTime < stage.TimeAfterDeath)
                break;
            efficiency = stage.Efficiency;
        }
        return efficiency;
    }
}

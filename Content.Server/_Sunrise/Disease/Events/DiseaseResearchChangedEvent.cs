namespace Content.Server._Sunrise.Disease.Events;

/// <summary>
/// Поднимается, когда в деле по любому штамму что-то изменилось: новые данные анализов,
/// выросший прогресс исследования или найденный рецепт.
/// </summary>
/// <remarks>
/// Дело общее для станции, поэтому машины обновляют по нему своё состояние все разом,
/// а не только та, с которой сейчас работают.
/// </remarks>
[ByRefEvent]
public readonly record struct DiseaseResearchChangedEvent;

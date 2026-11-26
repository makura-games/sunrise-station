namespace Content.Shared._Sunrise.Medical;

/// <summary>
/// Позволяет изменить показатели питания анализатора в процентах.
/// </summary>
[ByRefEvent]
public record struct GetHealthAnalyzerSatiationEvent(float HungerLevel, float ThirstLevel);

using Robust.Shared.Serialization;
using Content.Shared._Sunrise.Disease.Prototypes;

namespace Content.Shared._Sunrise.Disease.Microscope;

/// <summary>Ключ интерфейса микроскопа для разработки штаммов.</summary>
[Serializable, NetSerializable]
public enum DiseaseMicroscopeUiKey : byte
{
    Key,
}

/// <summary>Инструменты партии: каждый ход тратит реагент и даёт конкуренту ответить.</summary>
[Serializable, NetSerializable]
public enum DiseaseMicroscopeTool : byte
{
    Nutrient,
    Inhibitor,
}

/// <summary>
/// Засевает поле партии за дополнительный путь передачи. Предсказывается: устаревшее нажатие
/// отсекает счётчик партий, а не ревизия окна.
/// </summary>
[Serializable, NetSerializable]
public sealed class DiseaseMicroscopeTransmissionMessage(DiseaseTransmissionRoute route, int matchCounter) : BoundUserInterfaceMessage
{
    public DiseaseTransmissionRoute Route = route;
    public int MatchCounter = matchCounter;
}

/// <summary>
/// Засевает поле под выбранный симптом текущей стадии. Предсказывается: устаревшее нажатие
/// отсекает счётчик партий, а не ревизия окна.
/// </summary>
[Serializable, NetSerializable]
public sealed class DiseaseMicroscopePrepareMessage(int symptom, int matchCounter) : BoundUserInterfaceMessage
{
    public int Symptom = symptom;
    public int MatchCounter = matchCounter;
}

/// <summary>
/// Применяет инструмент к одной клетке поля. Ход предсказывается, поэтому устаревший ход
/// отсекает номер хода, а не ревизия окна: ходы идут подряд, не дожидаясь подтверждения.
/// </summary>
[Serializable, NetSerializable]
public sealed class DiseaseMicroscopeApplyMessage(DiseaseMicroscopeTool tool, int x, int y, int turn) : BoundUserInterfaceMessage
{
    public DiseaseMicroscopeTool Tool = tool;
    public int X = x;
    public int Y = y;
    public int Turn = turn;
}

/// <summary>
/// Тратит прогноз партии: до следующего хода окно показывает ответ конкурента.
/// Предсказывается, устаревшее нажатие отсекает номер хода.
/// </summary>
[Serializable, NetSerializable]
public sealed class DiseaseMicroscopeForecastMessage(int turn) : BoundUserInterfaceMessage
{
    public int Turn = turn;
}

/// <summary>Останавливает партию и закрепляет результат стадии в геноме заготовки.</summary>
[Serializable, NetSerializable]
public sealed class DiseaseMicroscopeFinishMessage(int revision) : BoundUserInterfaceMessage
{
    public int Revision = revision;
}

/// <summary>Закрывает стадию окончательно и переводит заготовку к следующей.</summary>
[Serializable, NetSerializable]
public sealed class DiseaseMicroscopeAdvanceMessage(int revision) : BoundUserInterfaceMessage
{
    public int Revision = revision;
}

/// <summary>Выпускает собранный геном отдельным штаммом, сохраняя исходную заготовку.</summary>
[Serializable, NetSerializable]
public sealed class DiseaseMicroscopeProduceMessage(int revision) : BoundUserInterfaceMessage
{
    public int Revision = revision;
}

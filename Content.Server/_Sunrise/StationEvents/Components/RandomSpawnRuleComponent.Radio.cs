using Content.Shared.Radio;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Пространство имён намеренно соответствует расширяемому vanilla-компоненту.
namespace Content.Server.StationEvents.Components;

public sealed partial class RandomSpawnRuleComponent
{
    /// <summary>
    /// Настройки радио-анонса, отправляемого от имени созданной сущности.
    /// </summary>
    [DataField]
    public RandomSpawnRuleRadioMessage? RadioMessage;
}

/// <summary>
/// Описывает канал и локализуемое сообщение радио-анонса случайного спавна.
/// </summary>
/// <param name="Channel">Канал, в который отправляется сообщение.</param>
/// <param name="Message">Сообщение с аргументом локализации <c>location</c>.</param>
[DataRecord]
public sealed partial record RandomSpawnRuleRadioMessage(
    [field: DataField(required: true)]
    ProtoId<RadioChannelPrototype> Channel,
    [field: DataField(required: true)]
    LocId Message
);

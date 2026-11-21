using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Выбирает болезни источника при появлении предмета, а не в прототипе.
/// </summary>
/// <remarks>
/// Нужен там, где заражённый предмет приходит не от правила события, а обычным путём:
/// грузовым заказом, из ящика или из спавнера. Без этого партия груза выдавала бы
/// один и тот же штамм раунд за раундом. Живёт в shared только ради загрузки прототипов,
/// вся работа на сервере.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseRandomSourceComponent : Component
{
    /// <summary>Кандидаты, из которых выбирается зараза предмета.</summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Templates = new();

    /// <summary>Сколько разных болезней достанется источнику.</summary>
    [DataField]
    public int Count = 1;
}

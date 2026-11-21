using Content.Server._Sunrise.StationEvents.Events;
using Robust.Shared.Audio;

namespace Content.Server._Sunrise.StationEvents.Components;

/// <summary>
/// Разгерметизация сосуда для исследования вирусов: облако культуры на месте и вспышка по станции.
/// </summary>
/// <remarks>
/// Событие само выбирает сосуд с заразной культурой на станции. Если такого нет,
/// ничего не происходит и объявление не звучит. Поломка сосуда игроками это событие не запускает.
/// </remarks>
[RegisterComponent, Access(typeof(DiseaseVesselBreachRule))]
public sealed partial class DiseaseVesselBreachRuleComponent : Component
{
    /// <summary>
    /// Сколько человек заражается минимум, помимо попавших в облако.
    /// </summary>
    [DataField]
    public int MinInfected = 1;

    /// <summary>
    /// Сколько человек заражается максимум, помимо попавших в облако.
    /// </summary>
    [DataField]
    public int MaxInfected = 2;

    /// <summary>
    /// Объявление об аварии. Звучит, только если авария действительно случилась.
    /// </summary>
    [DataField]
    public LocId Announcement = "station-event-disease-vessel-breach-announcement";

    /// <summary>
    /// Звук объявления.
    /// </summary>
    [DataField]
    public SoundSpecifier? AnnouncementSound = new SoundPathSpecifier("/Audio/Announcements/attention.ogg");

    /// <summary>
    /// Цвет объявления.
    /// </summary>
    [DataField]
    public Color AnnouncementColor = Color.Gold;
}

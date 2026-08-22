using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cloning.Events;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Jobs;

/// <summary>
/// Серверная система для альтернативных названий должностей.
/// Читает выбранный альтернативный титул из профиля игрока и применяет его
/// к ID-карте и записи в манифесте экипажа при спавне.
/// </summary>
public sealed partial class AlternativeJobTitleSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedIdCardSystem _card = default!;
    [Dependency] private StationRecordsSystem _records = default!;

    public override void Initialize()
    {
        // Обновляем запись в манифесте экипажа после создания
        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(OnAfterGeneralRecordCreated);
        // Обновляем ID-карту после спавна
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        // Копируем название должности при клонировании
        SubscribeLocalEvent<PdaComponent, CloningItemEvent>(OnClonePda);
    }

    /// <summary>
    /// Возвращает локализованный альтернативный титул для должности из профиля,
    /// или null если титул не выбран или невалиден.
    /// </summary>
    private string? GetAlternativeTitle(HumanoidCharacterProfile profile, string jobId)
    {
        if (!profile.JobAlternativeTitles.TryGetValue(jobId, out var altTitleLocId))
            return null;

        if (!_prototype.TryIndex<JobPrototype>(jobId, out var jobProto))
            return null;

        if (!jobProto.AlternativeTitles.Contains(altTitleLocId))
            return null;

        return Loc.GetString(altTitleLocId);
    }

    private void OnAfterGeneralRecordCreated(AfterGeneralRecordCreatedEvent ev)
    {
        if (string.IsNullOrEmpty(ev.Record.JobPrototype))
            return;

        var title = GetAlternativeTitle(ev.Profile, ev.Record.JobPrototype);
        if (title == null)
            return;

        ev.Record.JobTitle = title;
        _records.Synchronize(ev.Key);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null)
            return;

        var title = GetAlternativeTitle(ev.Profile, ev.JobId);
        if (title == null)
            return;

        // Находим ID-карту игрока
        if (!_card.TryFindIdCard(ev.Mob, out var idCard))
            return;

        _card.TryChangeJobTitle(idCard, title);
    }

    private void OnClonePda(Entity<PdaComponent> ent, ref CloningItemEvent args)
    {
        if (ent.Comp.ContainedId is not { } originalCardUid)
            return;

        if (!TryComp<IdCardComponent>(originalCardUid, out var originalCard))
            return;

        if (!TryComp<PdaComponent>(args.CloneUid, out var clonePda))
            return;

        if (clonePda.ContainedId is not { } cloneCardUid)
            return;

        if (!TryComp<IdCardComponent>(cloneCardUid, out var cloneCardComp))
            return;

        _card.TryChangeJobTitle(cloneCardUid, originalCard.LocalizedJobTitle, cloneCardComp);
    }
}

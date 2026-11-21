using Content.Server.Popups;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Systems;
using Content.Shared.Buckle.Components;
using Robust.Server.Audio;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Серверная часть стерилизующей арки: гасит заразу на вещах прошедшего и пищит, если он болен.
/// </summary>
/// <remarks>
/// Проверка на заразу не предсказывается: болезни живут только на сервере, и клиент не должен
/// узнавать о них раньше, чем арка запищит для всех.
/// </remarks>
public sealed partial class DiseaseSterilizerSystem : SharedDiseaseSterilizerSystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DiseaseDisinfectantSystem _disinfectant = default!;
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private PopupSystem _popup = default!;

    private readonly List<EntityUid> _targets = new();

    protected override void OnPassed(Entity<DiseaseSterilizerComponent> ent, EntityUid target, bool sprayed)
    {
        // Каталку арка проверяет вместе с тем, кто на ней лежит.
        _targets.Clear();
        _targets.Add(target);
        if (TryComp<StrapComponent>(target, out var strap))
            _targets.AddRange(strap.BuckledEntities);

        var cleaned = 0;
        var infected = false;
        foreach (var subject in _targets)
        {
            if (sprayed)
            {
                // Сначала вещи: с тела живого снимать нечего, а у мёртвого заодно очищается и снаряжение.
                cleaned += _disinfectant.DisinfectCarried(subject);
                if (_disinfectant.TryDisinfect(subject, quiet: true))
                    cleaned++;
            }

            // Мёртвого носителя распыление уже вылечило, поэтому тревога только по оставшимся болезням.
            infected |= _disease.GetDiseases(subject).Count > 0;
        }

        if (sprayed)
        {
            if (ent.Comp.MistPrototype is { } mist)
                Spawn(mist, Transform(ent).Coordinates);

            if (cleaned > 0)
                _popup.PopupEntity(Loc.GetString("disease-sterilizer-popup-cleaned"), target, target);
        }

        if (!infected)
            return;

        _audio.PlayPvs(ent.Comp.AlarmSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("disease-sterilizer-popup-alarm"), ent.Owner);
        SetState(ent, DiseaseSterilizerState.Alarm);
    }
}

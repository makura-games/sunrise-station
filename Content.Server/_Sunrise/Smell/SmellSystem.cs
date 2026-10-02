using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Popups;
using Content.Shared._Sunrise.Smell;
using Content.Shared._Sunrise.Smell.Components;
using Content.Shared._Sunrise.Smell.Prototypes;
using Content.Shared.ActionBlocker;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Verbs;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.Smell;

/// <summary>
/// The "smelling" system: the smell verb, access checks, lazy recalculation of
/// temporary scents and readable description output. Scent granting (sources) lives in
/// ScentAcquisitionSystem; the shared prototype cache is SmellPrototypeCacheSystem.
/// </summary>
public sealed partial class SmellSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SmellPrototypeCacheSystem _cache = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private EntityQuery<SmellComponent> _smellQuery = default!;
    [Dependency] private EntityQuery<ScentComponent> _scentQuery = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<ScentComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
    }

    /// <summary>
    /// Adds the "smell" verb to a target with ScentComponent if the smeller is
    /// itself capable of smelling (has SmellComponent) and passes access/interaction checks.
    /// </summary>
    private void OnGetInteractionVerbs(
        Entity<ScentComponent> target,
        ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!HasComp<SmellComponent>(args.User))
            return;

        EntityUid user = args.User;

        args.Verbs.Add(new InteractionVerb
        {
            Text = _loc.GetString("smell-verb"),
            TextStyleClass = "Default",
            Act = () => TrySmell(user, target.AsNullable())
        });
    }

    /// <summary>
    /// Entry point: smell capability check and scent description output.
    /// Some failed CanSmell checks show a popup.
    /// </summary>
    public bool TrySmell(Entity<SmellComponent?> user, Entity<ScentComponent?> target)
    {
        if (!ResolveSmellComponents(ref user, ref target))
            return false;

        if (!CanSmell(user, target, out var reason))
        {
            if (reason != null)
                _popup.PopupEntity(_loc.GetString(reason), user, user);
            return false;
        }

        DoSmell(user, target!);
        return true;
    }

    /// <summary>
    /// Resolves both optional component references at the public boundary and
    /// fails fast if either component is missing.
    /// </summary>
    private bool ResolveSmellComponents(ref Entity<SmellComponent?> user, ref Entity<ScentComponent?> target)
    {
        return _smellQuery.Resolve(ref user, false) && _scentQuery.Resolve(ref target, false);
    }

    /// <summary>
    /// Smell capability check; returns false and a failure reason.
    /// The reason is null for a silent rejection (no suitable message).
    /// </summary>
    public bool CanSmell(Entity<SmellComponent?> user, Entity<ScentComponent?> target, out LocId? reason)
    {
        reason = null;

        if (!ResolveSmellComponents(ref user, ref target))
            return false;

        if (IsMaskEquipped(user) || IsHeadSealed(user))
        {
            reason = "smell-blocked-by-gear";
            return false;
        }

        if (IsHardsuitSealed(target))
        {
            reason = "smell-blocked-by-target-gear";
            return false;
        }

        if (!_actionBlocker.CanInteract(user, target) ||
            !_interaction.InRangeUnobstructed(user.Owner, target.Owner))
            return false;

        return true;
    }

    /// <summary>
    /// Whether an equipped, non-toggled item occupies the mask slot. A toggled-down mask
    /// (MaskComponent.IsToggled) does not cover the nose and does not block smelling.
    /// </summary>
    private bool IsMaskEquipped(EntityUid uid)
    {
        if (!_inventory.TryGetSlotEntity(uid, "mask", out var maskEntity))
            return false;

        return TryComp<MaskComponent>(maskEntity, out var mask)
            && !mask.IsToggled;
    }

    /// <summary>
    /// Whether the person wears a closed pressure-proof helmet.
    /// A closed helmet alone blocks smelling — the nose is covered.
    /// </summary>
    private bool IsHeadSealed(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "head", out var helmet)
               && HasComp<PressureProtectionComponent>(helmet);
    }

    /// <summary>
    /// Whether the person wears a fully sealed kit: both the outer clothing and the
    /// helmet must be pressure-protected (hardsuits and separate EVA kits).
    /// Used for the TARGET — to hide their scent the whole body must be enclosed;
    /// the smeller only needs their own head covered (see IsHeadSealed).
    /// </summary>
    private bool IsHardsuitSealed(EntityUid uid)
    {
        if (!_inventory.TryGetSlotEntity(uid, "outerClothing", out var suitEntity))
            return false;

        if (!_inventory.TryGetSlotEntity(uid, "head", out var helmetEntity))
            return false;

        return HasComp<PressureProtectionComponent>(suitEntity)
               && HasComp<PressureProtectionComponent>(helmetEntity);
    }

    /// <summary>
    /// Performs smelling: lazy mask removal on expiry, assembling the scent
    /// description (base + temporary) and sending the tooltip to the smeller.
    /// </summary>
    private void DoSmell(EntityUid user, Entity<ScentComponent> target)
    {
        FormattedMessage message = new();

        // Ленивое снятие маскировки: если время истекло — маска пропадает сама.
        // При активной маске основной запах скрыт, но временные запахи всё ещё показываются.
        if (IsMasked(target))
            message.AddMarkupOrThrow($"[color={_cache.Config.MaskedScentColor.ToHex()}]{_loc.GetString("smell-result-masked")}[/color]");
        else
            AppendBaseAndPersonalScents(message, target);

        AppendTemporaryScents(message, GetTemporaryScentNotes(user, target));

        _examine.SendExamineTooltip(user, target, message, false, false);
    }

    /// <summary>
    /// Appends the target's base scent to the message: static (BaseScents) and personal
    /// (generated from the profile). If both are missing — the "no scent" line.
    /// </summary>
    private void AppendBaseAndPersonalScents(FormattedMessage message, Entity<ScentComponent> target)
    {
        List<string> staticNotes = [];

        foreach (ProtoId<ScentPrototype> scentId in target.Comp.BaseScents)
        {
            ScentPrototype scent = _prototypes.Index<ScentPrototype>(scentId);
            staticNotes.Add(GetScentDescription(scent));
        }

        ScentSignature? signature = GetPersonalSignature(target);

        // --- ОСНОВНОЙ запах (статичный + личный) всегда в начале ---
        if (staticNotes.Count > 0)
        {
            message.AddMarkupOrThrow(_loc.GetString(
                "smell-result-static",
                ("notes", string.Join(", ", staticNotes))));
        }

        if (signature != null)
        {
            if (staticNotes.Count > 0)
                message.AddMarkupOrThrow("\n");

            List<string> personalNotes = [];

            foreach (LocId note in signature.Notes)
            {
                personalNotes.Add(_loc.GetString(note));
            }

            message.AddMarkupOrThrow(_loc.GetString(
                "smell-result-personal",
                ("color", signature.Color.ToHex()),
                ("notes", string.Join(", ", personalNotes))));
        }

        if (staticNotes.Count == 0 && signature == null)
        {
            message.AddMarkupOrThrow(_loc.GetString("smell-result-none"));
        }
    }

    /// <summary>
    /// Appends temporary scents to the message, grouped by strength
    /// (Strong -> Medium -> Faint), with a header above the block.
    /// </summary>
    private void AppendTemporaryScents(
        FormattedMessage message,
        List<(ScentStrength group, float intensity, string text)> tempNotes)
    {
        if (tempNotes.Count == 0)
            return;

        message.AddMarkupOrThrow("\n");
        message.AddMarkupOrThrow(_loc.GetString("smell-result-temporary-header"));

        // Отдельная строка на каждую непустую группу, в порядке Strong -> Medium -> Faint.
        foreach (ScentStrength group in Enum.GetValues<ScentStrength>())
        {
            var groupLines = tempNotes
                .Where(n => n.group == group)
                .Select(n => n.text)
                .ToList();

            if (groupLines.Count == 0)
                continue;

            message.AddMarkupOrThrow("\n");
            message.AddMarkupOrThrow(_loc.GetString(
                $"smell-strength-{group.ToString().ToLowerInvariant()}",
                ("notes", string.Join(", ", groupLines))));
        }
    }

    /// <summary>
    /// Whether the temporary mask is active. If expired — the mask is removed lazily
    /// (on the next smelling) and counts as inactive.
    /// </summary>
    private bool IsMasked(Entity<ScentComponent> target)
    {
        if (!target.Comp.Masked)
            return false;

        if (_timing.CurTime >= target.Comp.MaskUntil)
        {
            target.Comp.Masked = false;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds the target's personal scent signature: color and notes deterministically
    /// generated from the profile (PersonalScentProfile) and character traits
    /// (name, age, gender, voice). The same seed always yields the same scent.
    /// </summary>
    private ScentSignature? GetPersonalSignature(Entity<ScentComponent> target)
    {
        if (target.Comp.PersonalScentProfile is not { } profileId)
            return null;

        PersonalScentProfilePrototype profile =
            _prototypes.Index<PersonalScentProfilePrototype>(profileId);

        var name = Name(target.Owner);

        PersonalCharacteristics? characteristics = null;

        if (TryComp<HumanoidProfileComponent>(target.Owner, out HumanoidProfileComponent? humanoidProfile))
        {
            string voice = string.Empty;
            if (TryComp<TTSComponent>(target.Owner, out var tts))
                voice = tts.VoicePrototypeId?.ToString() ?? string.Empty;

            characteristics = new PersonalCharacteristics
            {
                Age = humanoidProfile.Age,
                Gender = humanoidProfile.Gender,
                Voice = voice,
            };
        }

        var seed = name;
        if (characteristics != null)
        {
            seed += $":{characteristics.Age}:{characteristics.Gender}:{characteristics.Voice}";
        }

        return ScentSignatureGenerator.Generate(seed, profile);
    }

    /// <summary>
    /// Lazily recalculates temporary scents: drops expired ones, determines the
    /// strength group by age and sorts by intensity.
    /// </summary>
    private List<(ScentStrength group, float intensity, string text)> GetTemporaryScentNotes(
        EntityUid user, Entity<ScentComponent> target)
    {
        var result = new List<(ScentStrength group, float intensity, string text)>();
        var now = _timing.CurTime;

        // Проходим с конца, чтобы безопасно удалять мёртвые записи.
        for (int i = target.Comp.TemporaryScents.Count - 1; i >= 0; i--)
        {
            var entry = target.Comp.TemporaryScents[i];

            // Защита от деления на ноль и от отрицательной длительности.
            if (entry.Duration <= TimeSpan.Zero)
            {
                target.Comp.TemporaryScents.RemoveAt(i);
                continue;
            }

            // Протух: убираем за ненадобностью (часть ленивой очистки).
            var lifetime = entry.StartTime + entry.Duration;
            if (lifetime <= now)
            {
                target.Comp.TemporaryScents.RemoveAt(i);
                continue;
            }

            var age = now - entry.StartTime;
            var ratio = (float) (age / entry.Duration);
            var scentProto = _prototypes.Index<ScentPrototype>(entry.Scent);
            result.Add((GetScentStrength(ratio), scentProto.Intensity, GetTemporaryScentText(user, target, entry)));
        }

        // Запахи состояний (пьянство, наркотрип): проверяются лениво по активным
        // статус-эффектам носителя. Сила — по положению внутри времени эффекта.
        AddStatusScents(target, result);

        // Свежие (сильные) группы раньше, внутри группы — по убыванию интенсивности.
        result.Sort((a, b) =>
        {
            var cmp = b.group.CompareTo(a.group);
            return cmp != 0 ? cmp : b.intensity.CompareTo(a.intensity);
        });

        return result;
    }

    /// <summary>
    /// For each status scent from YAML, checks whether the corresponding status
    /// effect is active on the bearer and adds the scent. Strength (Strong/Medium/Faint)
    /// follows the position within the effect duration: the fresher the effect,
    /// the stronger the smell; it fades towards the end.
    /// Normalized by the effect's full duration rather than a fixed threshold.
    /// </summary>
    private void AddStatusScents(Entity<ScentComponent> target, List<(ScentStrength group, float intensity, string text)> result)
    {
        var now = _timing.CurTime;

        // У цели нет контейнера статус-эффектов -> ничего не проверяем (иначе TryGetTime
        // логирует ошибку Resolve на каждую итерацию для сущностей без StatusEffectContainer).
        if (!HasComp<StatusEffectContainerComponent>(target))
            return;

        foreach (var proto in _cache.StatusScentProtos)
        {
            if (!_statusEffects.TryGetTime(target, proto.StatusEffect, out var time))
                continue;

            // Эффект без конечного времени считается длящимся бесконечно -> полная сила.
            if (time.EndEffectTime is not { } endTime)
            {
                var scentEndless = _prototypes.Index<ScentPrototype>(proto.Scent);
                result.Add((ScentStrength.Strong, scentEndless.Intensity, GetScentDescription(scentEndless)));
                continue;
            }

            var remaining = endTime - now;
            if (remaining <= TimeSpan.Zero)
                continue; // эффект уже фактически истёк.

            // Длительность эффекта; если время старта неизвестно, total = 0
            // и эффект считается сильным (см. блок ниже).
            var total = endTime - (time.StartEffectTime ?? endTime);
            if (total <= TimeSpan.Zero)
            {
                var scentFullyStrong = _prototypes.Index<ScentPrototype>(proto.Scent);
                result.Add((ScentStrength.Strong, scentFullyStrong.Intensity, GetScentDescription(scentFullyStrong)));
                continue;
            }

            // Чем больше осталось до конца, тем выше ratio (0 = конец, 1 = начало).
            var ratio = (float) Math.Clamp(remaining.TotalSeconds / total.TotalSeconds, 0.0, 1.0);
            var scent = _prototypes.Index<ScentPrototype>(proto.Scent);
            var strength = GetScentStrength(1f - ratio);

            // Короткий эффект не должен вонять сильно: его максимум — Medium, и чем короче,
            // тем слабее даже на пике (плавное затухание от Strong к Medium по длительности).
            if (proto.MinDurationForStrong > TimeSpan.Zero)
            {
                var durationScale = (float) Math.Clamp(
                    total.TotalSeconds / proto.MinDurationForStrong.TotalSeconds, 0.0, 1.0);
                if (strength == ScentStrength.Strong && durationScale < 1f)
                    strength = durationScale >= 0.5f ? ScentStrength.Medium : ScentStrength.Faint;
            }

            result.Add((strength, scent.Intensity, GetScentDescription(scent)));
        }
    }

    /// <summary>
    /// Returns the temporary scent text.
    /// </summary>
    private string GetTemporaryScentText(EntityUid user, Entity<ScentComponent> target, ActiveTemporaryScent entry)
    {
        var scent = _prototypes.Index<ScentPrototype>(entry.Scent);

        return GetScentDescription(scent);
    }

    /// <summary>
    /// Returns the localized scent description, wrapped in bold if the scent prototype
    /// has the Fat flag set (an accenting/pungent smell).
    /// </summary>
    private string GetScentDescription(ScentPrototype scent, LocId? descriptionOverride = null)
    {
        var text = _loc.GetString(descriptionOverride ?? scent.Description);
        if (scent.Color is { } color)
            text = $"[color={color.ToHex()}]{text}[/color]";
        return scent.Fat ? $"[bold]{text}[/bold]" : text;
    }

    /// <summary>
    /// Determines the strength group by the lived fraction of the duration
    /// (0 = just appeared, 1 = almost expired).
    /// </summary>
    private static ScentStrength GetScentStrength(float ratio)
    {
        if (ratio < 0.33f) return ScentStrength.Strong;
        if (ratio < 0.66f) return ScentStrength.Medium;
        return ScentStrength.Faint;
    }

    /// <summary>
    /// Internal record collecting the scent bearer's traits.
    /// </summary>
    private sealed record PersonalCharacteristics
    {
        public int Age { get; init; }
        public Gender Gender { get; init; }
        public string Voice { get; init; } = string.Empty;
    }
}

/// <summary>
/// The three scent strength groups used for ordering in descriptions.
/// </summary>
public enum ScentStrength
{
    Strong,
    Medium,
    Faint,
}

using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Применяет <see cref="DiseaseProtectionComponent"/> к попыткам заражения:
/// как на самом носителе, так и на его надетой экипировке.
/// </summary>
/// <remarks>
/// Показатели защиты видны при осмотре отдельной кнопкой, по образцу показателей брони.
/// </remarks>
public sealed partial class DiseaseProtectionSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private EntityQuery<MaskComponent> _maskQuery = default!;
    [Dependency] private EntityQuery<DiseaseResidueComponent> _residueQuery = default!;

    /// <summary>
    /// Порядок строк в осмотре: от самого частого пути к самому редкому.
    /// </summary>
    private static readonly (DiseaseTransmissionRoute Route, DiseaseContactKind Contact, LocId Name)[] ExamineLines =
    [
        (DiseaseTransmissionRoute.Droplet, DiseaseContactKind.None, "disease-protection-type-droplet"),
        (DiseaseTransmissionRoute.Contact, DiseaseContactKind.Touch, "disease-protection-type-touch"),
        (DiseaseTransmissionRoute.Contact, DiseaseContactKind.Bite, "disease-protection-type-bite"),
        (DiseaseTransmissionRoute.Contact, DiseaseContactKind.Step, "disease-protection-type-step"),
        (DiseaseTransmissionRoute.Ingestion, DiseaseContactKind.None, "disease-protection-type-ingestion"),
        (DiseaseTransmissionRoute.Injection, DiseaseContactKind.None, "disease-protection-type-injection"),
    ];

    private const string ExamineIcon = "/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png";

    [SubscribeLocalEvent]
    private void OnExposureAttempt(Entity<DiseaseProtectionComponent> ent, ref DiseaseExposureAttemptEvent args)
    {
        ApplyProtection(ent.Comp, ref args);
    }

    [SubscribeLocalEvent]
    private void OnRelayedExposureAttempt(Entity<DiseaseProtectionComponent> ent, ref InventoryRelayedEvent<DiseaseExposureAttemptEvent> args)
    {
        // Сдвинутая на подбородок маска ничего не фильтрует.
        if (_maskQuery.TryComp(ent, out var mask) && mask.IsToggled)
            return;

        ApplyProtection(ent.Comp, ref args.Args);
    }

    private static void ApplyProtection(DiseaseProtectionComponent protection, ref DiseaseExposureAttemptEvent args)
    {
        if ((protection.Routes & args.Route) == 0)
            return;

        // Перчатки не спасают от укуса, а скафандр — от касания голой рукой.
        if (args.Route == DiseaseTransmissionRoute.Contact && (protection.ContactKinds & args.Contact) == 0)
            return;

        args.Multiplier *= protection.Coefficient;
    }

    [SubscribeLocalEvent]
    private void OnExamineVerb(Entity<DiseaseProtectionComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Беспалые перчатки и часы несут компонент, но ни от чего не защищают: кнопка им не нужна.
        if (GetExamine(ent) is not { } markup)
            return;

        _examine.AddDetailedExamineVerb(args, ent.Comp, markup,
            Loc.GetString("disease-protection-examinable-verb-text"), ExamineIcon,
            Loc.GetString("disease-protection-examinable-verb-message"));
    }

    /// <summary>
    /// Собирает текст осмотра: насколько вещь снижает риск на каждом пути и собирает ли она заразу.
    /// </summary>
    /// <returns>Null, если показывать нечего.</returns>
    private FormattedMessage? GetExamine(Entity<DiseaseProtectionComponent> ent)
    {
        var protection = ent.Comp;
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("disease-protection-examine"));

        var lines = 0;
        var touch = false;
        if (protection.Coefficient < 1f)
        {
            foreach (var (route, contact, name) in ExamineLines)
            {
                if ((protection.Routes & route) == 0)
                    continue;

                if (contact != DiseaseContactKind.None && (protection.ContactKinds & contact) == 0)
                    continue;

                touch |= contact == DiseaseContactKind.Touch;
                msg.PushNewline();
                msg.AddMarkupOrThrow(Loc.GetString("disease-protection-coefficient-value",
                    ("type", Loc.GetString(name)),
                    ("value", MathF.Round((1f - Math.Max(protection.Coefficient, 0f)) * 100f, 1))));
                lines++;
            }
        }

        // Вещь, которой трогают, отдельно сообщает, остаётся ли на ней зараза.
        if (_residueQuery.TryComp(ent, out var residue))
        {
            if (residue.PickUpChance > 0f)
            {
                msg.PushNewline();
                msg.AddMarkupOrThrow(Loc.GetString("disease-protection-residue-carrier",
                    ("chance", MathF.Round(residue.ContactChance * 100f, 1))));
                lines++;
            }
            else if (touch)
            {
                msg.PushNewline();
                msg.AddMarkupOrThrow(Loc.GetString("disease-protection-residue-clean"));
                lines++;
            }
        }

        return lines > 0 ? msg : null;
    }
}

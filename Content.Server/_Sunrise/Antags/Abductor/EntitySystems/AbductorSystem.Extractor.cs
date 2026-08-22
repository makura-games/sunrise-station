using Content.Shared._Sunrise.Antags.Abductor;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.DoAfter;
using Content.Shared.Body.Systems;
using Content.Shared.Starlight.Medical.Surgery;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;

namespace Content.Server._Sunrise.Antags.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{

    [Dependency] private BodySystem _body = default!;
    [Dependency] private ISharedAdminLogManager _admin = default!;

    public void InitializeExtractor()
    {
        SubscribeLocalEvent<AbductorExtractorComponent, AfterInteractEvent>(OnExtractorInteract);

        SubscribeLocalEvent<AbductorExtractorComponent, AbductorExtractDoAfterEvent>(OnExtractDoAfter);
    }

    private void OnExtractorInteract(Entity<AbductorExtractorComponent> ent, ref AfterInteractEvent args)
    {
        if (!_actionBlockerSystem.CanInstrumentInteract(args.User, args.Used, args.Target)
            || !args.Target.HasValue
            || !_body.TryGetOrganWithComponent<OrganHeartComponent>(args.Target.Value, out _))
            return;

        if (HasComp<SurgeryTargetComponent>(args.Target))
            Extract(ent, args.Target.Value, args.User);
    }

    public void Extract(Entity<AbductorExtractorComponent> ent, EntityUid target, EntityUid user)
    {
        var time = TimeSpan.FromSeconds(2);

        var doAfter = new DoAfterArgs(EntityManager, user, time, new AbductorExtractDoAfterEvent(), ent, target, ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1f
        };
        _admin.Add(LogType.InteractUsing, LogImpact.Low, $"{ToPrettyString(user)} trying to use extractor {ToPrettyString(ent.Owner)} for extracting heart from {ToPrettyString(target)}.");
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnExtractDoAfter(Entity<AbductorExtractorComponent> ent, ref AbductorExtractDoAfterEvent args)
    {
        if (args.Target is not { } target ||
            !_body.TryGetOrganWithComponent<OrganHeartComponent>(target, out var heart))
            return;

        _admin.Add(LogType.InteractUsing, LogImpact.Low, $"Heart successfully extracted from {ToPrettyString(target)} using {ToPrettyString(ent.Owner)} by {ToPrettyString(args.User)}");

        _body.TryRemoveOrgan((heart, Comp<OrganComponent>(heart)));
    }
}

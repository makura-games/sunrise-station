using Content.Shared._Sunrise.Abilities;
using Content.Shared.Actions;
using Content.Shared.Cuffs;
using Content.Shared.DoAfter;

namespace Content.Server._Sunrise.Abilities;
public sealed partial class BorgCuffedSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgCuffedComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BorgCuffedComponent, BorgCuffedActionEvent>(OnCuffed);
        SubscribeLocalEvent<BorgCuffedComponent, BorgCuffedDoAfterEvent>(OnCuffedDoAfter);
    }

    private void OnInit(EntityUid uid, BorgCuffedComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, component.CuffActionId);
    }

    private void OnCuffed(EntityUid uid, BorgCuffedComponent component, BorgCuffedActionEvent args)
    {
        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager ,uid, component.CuffTime,
            new BorgCuffedDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        });

        args.Handled = true;
    }

    private void OnCuffedDoAfter(EntityUid uid, BorgCuffedComponent component,
        BorgCuffedDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        var cuffs = Spawn(component.CableCuffsId, Transform(uid).Coordinates);

        if (!_cuffable.TryCuffingNow(args.Args.User, args.Args.Target.Value, cuffs))
            QueueDel(cuffs);
    }
}

using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Roles;
using Content.Server._Sunrise.Antags.SELF;
using Content.Server._Sunrise.GameTicking.Rules.Components;
using Content.Server._Sunrise.Roles.Components;

namespace Content.Server._Sunrise.GameTicking.Rules;

public sealed partial class SELFRuleSystem : GameRuleSystem<SELFRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SELFRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
        SubscribeLocalEvent<SELFAgentRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void OnAfterAntagSelected(Entity<SELFRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        _antag.SendBriefing(args.EntityUid, MakeBriefing(), null, null);
        EnsureComp<SELFAgentComponent>(args.EntityUid);
    }

    private void OnGetBriefing(Entity<SELFAgentRoleComponent> role, ref GetBriefingEvent args)
    {
        if (args.Mind.Comp.OwnedEntity is null)
            return;

        args.Append(MakeBriefing());
    }

    private string MakeBriefing() =>
        $"{Loc.GetString("self-role-greeting-human")}\n\n{Loc.GetString("self-role-greeting-equipment")}\n";
}

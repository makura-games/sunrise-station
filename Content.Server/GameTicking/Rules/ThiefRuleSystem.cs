using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Roles;
using Content.Shared.Humanoid;
using Content.Shared.Roles.Components;
using Content.Shared.NPC.Systems;

namespace Content.Server.GameTicking.Rules;

public sealed partial class ThiefRuleSystem : GameRuleSystem<ThiefRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThiefRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);

        SubscribeLocalEvent<ThiefRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    // Greeting upon thief activation
    private void AfterAntagSelected(Entity<ThiefRuleComponent> mindId, ref AfterAntagEntitySelectedEvent args)
    {
        var ent = args.EntityUid;
        _antag.SendBriefing(ent, MakeBriefing(ent), null, null);

        Log.Debug($"MakeThief {ToPrettyString(ent)} - Change faction");
        _npcFaction.RemoveFaction(ent, mindId.Comp.NanoTrasenFaction, false);
        _npcFaction.AddFaction(ent, mindId.Comp.SyndicateFaction);
    }

    // Character screen briefing
    private void OnGetBriefing(Entity<ThiefRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent is null)
            return;
        args.Append(MakeBriefing(ent.Value));
    }

    private string MakeBriefing(EntityUid ent)
    {
        var isHuman = HasComp<HumanoidProfileComponent>(ent);
        var briefing = isHuman
            ? Loc.GetString("thief-role-greeting-human")
            : Loc.GetString("thief-role-greeting-animal");

        if (isHuman)
            briefing += "\n \n" + Loc.GetString("thief-role-greeting-equipment") + "\n";

        return briefing;
    }
}

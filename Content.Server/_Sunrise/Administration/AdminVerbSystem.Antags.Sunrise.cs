using Content.Server.GameTicking.Rules.Components;
using Content.Shared.Administration;
using Content.Shared.Antag;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Content.Server._Sunrise.AssaultOps;
using Content.Server._Sunrise.FleshCult.GameRule;
using Content.Server._Sunrise.BloodCult.GameRule;
using Content.Server._Sunrise.GameTicking.Rules.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Administration.Systems;

/// <summary>
/// Sunrise — admin smite verb that assigns all Traitor antagonists a kill objective
/// targeting the selected player.
/// </summary>
public sealed partial class AdminVerbSystem
{
    private static readonly EntProtoId DefaultAssaultOpsRule = "AssaultOps";
    private static readonly EntProtoId DefaultFleshCultRule = "FleshCult";
    private static readonly ProtoId<AntagSpecifierPrototype> DefaultFleshCultistAntag = "SunriseFleshCultist";
    private static readonly EntProtoId DefaultSELFRule = "SiliconLiberation";
    private static readonly SpriteSpecifier SelfAgentVerbIcon =
        new SpriteSpecifier.Rsi(new ResPath("/Textures/_Sunrise/Interface/Misc/self_icon.rsi"), "icon");

    private void AddSunriseAntagVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.HasAdminFlag(player, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        var targetPlayer = targetActor.PlayerSession;


        // Assault Operative
        Verb assaultOperative = new()
        {
            Text = Loc.GetString("admin-verb-text-make-assault-operative"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Structures/Wallmounts/posters.rsi"),
                "poster46_contraband"),
            Act = () =>
            {
                _antag.ForceMakeAntag<AssaultOpsRuleComponent>(targetPlayer, DefaultAssaultOpsRule);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-assault-operative"),
        };
        args.Verbs.Add(assaultOperative);

        // Flesh Cultists
        Verb fleshCultist = new()
        {
            Text = "Make Flesh Cultist",
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Texture(
                new ResPath("_Sunrise/FleshCult/Interface/Actions/fleshCultistFleshHeart.png")),
            Act = () =>
            {
                _antag.ForceMakeAntag<FleshCultRuleComponent>(targetPlayer, DefaultFleshCultRule, DefaultFleshCultistAntag);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-flesh-cultist"),
        };
        args.Verbs.Add(fleshCultist);

        // Blood Cultists
        Verb bloodCultist = new()
        {
            Text = Loc.GetString("admin-verb-text-make-cultist"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Weapons/Melee/cult_dagger.rsi"), "icon"),
            Act = () =>
            {
                _antag.ForceMakeAntag<BloodCultRuleComponent>(targetPlayer, "BloodCult");
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-cultist"),
        };
        args.Verbs.Add(bloodCultist);

        // SELF agent
        Verb selfAgent = new()
        {
            Text = Loc.GetString("admin-verb-text-make-selfagent"),
            Category = VerbCategory.Antag,
            Icon = SelfAgentVerbIcon,
            Act = () =>
            {
                _antag.ForceMakeAntag<SELFRuleComponent>(targetPlayer, DefaultSELFRule);
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-selfagent"),
        };
        args.Verbs.Add(selfAgent);
    }
}

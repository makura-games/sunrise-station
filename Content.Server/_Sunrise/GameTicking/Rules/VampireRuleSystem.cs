using Content.Server.Antag;
using Content.Server.Bible.Components;
using Content.Server._Sunrise.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using System.Text;
using Robust.Shared.Audio;
using Content.Server.GameTicking.Rules;
using Content.Shared._Sunrise.Roles.Components;
using Content.Shared._Sunrise.Antags.Vampires; // Sunrise-Edit: VampireClassUiKey
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects; // Sunrise-Edit: UserInterfaceComponent для выбора класса

namespace Content.Server._Sunrise.GameTicking.Rules;

public sealed partial class VampireRuleSystem : GameRuleSystem<VampireRuleComponent>
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private ObjectivesSystem _objective = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!; // Sunrise-Edit: для выбора класса

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireRuleComponent, AfterAntagEntitySelectedEvent>(OnSelectAntag);
        SubscribeLocalEvent<VampireRuleComponent, ObjectivesTextPrependEvent>(OnTextPrepend);
    }

    private void OnSelectAntag(EntityUid uid, VampireRuleComponent comp, ref AfterAntagEntitySelectedEvent args)
        => MakeVampire(args.EntityUid, comp);

    public bool MakeVampire(EntityUid target, VampireRuleComponent rule)
    {
        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return false;

        // Капелланы на старте раунда не должны быть вампирами.
        if (HasComp<BibleUserComponent>(target))
        {
            _role.MindRemoveRole((mindId, mind), "MindRoleVampire");
            return false;
        }

        var meta = MetaData(target);
        var name = meta?.EntityName ?? "Unknown";
        var briefing = Loc.GetString("vampire-role-greeting", ("name", name));
        _antag.SendBriefing(target, briefing, Color.Yellow, rule.BriefingSound);

        if (
             _role.MindHasRole<VampireRoleComponent>(mindId, out var vampRole)
          && _role.MindHasRole<RoleBriefingComponent>(mindId, out var briefingComp)
        )
        {
            EnsureComp<RoleBriefingComponent>(vampRole.Value.Owner).Briefing = briefing;
        }

        EnsureComp<VampireComponent>(target);

        // Sunrise-Edit: регистрируем BUI выбора класса и чутья хищника, чтобы OpenUi работал
        var ui = EnsureComp<UserInterfaceComponent>(target);
        _ui.SetUi((target, ui), VampireClassUiKey.Key, new InterfaceData("VampireClassBui", 0f));
        _ui.SetUi((target, ui), VampireLocateUiKey.Key, new InterfaceData("VampireLocateBui", 0f));

        rule.VampireMinds.Add(mindId);

        return true;
    }

    private void OnTextPrepend(EntityUid uid, VampireRuleComponent comp, ref ObjectivesTextPrependEvent args)
    {
        var mostDrainedName = string.Empty;
        var mostDrained = 0f;
        var totalBlood = 0f;

        var query = EntityQueryEnumerator<VampireComponent>();
        while (query.MoveNext(out var vampUid, out var vamp))
        {
            if (!_mind.TryGetMind(vampUid, out var mindId, out var mind))
                continue;

            if (!TryComp(vampUid, out MetaDataComponent? meta))
                continue;

            if (!TryComp<VampireProgressionComponent>(vampUid, out var progression))
                continue;

            totalBlood += progression.TotalBlood;

            if (progression.TotalBlood > mostDrained)
            {
                mostDrained = progression.TotalBlood;
                mostDrainedName = _objective.GetTitle((mindId, mind), meta.EntityName);
            }
        }

        var sb = new StringBuilder();

        // Показываем статистику крови на основе общего выпитого объёма
        if (totalBlood > 0)
        {
            var category = totalBlood switch
            {
                < 500 => "low",
                < 1000 => "medium",
                < 2000 => "high",
                _ => "critical"
            };
            sb.AppendLine(Loc.GetString($"roundend-prepend-vampire-drained-{category}", ("blood", (int)totalBlood)));
        }

        sb.AppendLine(Loc.GetString($"roundend-prepend-vampire-drained{(!string.IsNullOrWhiteSpace(mostDrainedName) ? "-named" : "")}", ("name", mostDrainedName), ("number", (int)mostDrained)));

        args.Text = sb.ToString();
    }
}

using Content.Server.Chat.Systems;
using Content.Shared._Sunrise.Medical;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server._Sunrise.Medical;

/// <summary>
/// System for handling borg hypospray announcements
/// </summary>
public sealed partial class BorgHypospraySystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    /// <summary>
    /// Announces the reagent being injected by a borg hypospray
    /// </summary>
    public void TryAnnounceInjection(EntityUid hypospray, EntityUid user, EntityUid target, Entity<SolutionComponent> solution)
    {
        if (!TryComp<BorgHyposprayComponent>(hypospray, out var borgHypo))
            return;

        var currentTime = _timing.CurTime;
        if (currentTime < borgHypo.NextAnnouncementTime)
            return; // Still in cooldown

        // Get the main reagent being injected
        if (!TryGetMainReagent(solution.Comp.Solution, out var reagentId, out var reagentProto))
            return;

        // Set cooldown for next announcement
        borgHypo.NextAnnouncementTime = currentTime + borgHypo.AnnouncementCooldown;
        Dirty(hypospray, borgHypo);

        // Make the announcement
        var message = Loc.GetString("borg-hypospray-inject-announcement", 
            ("target", MetaData(target).EntityName ?? "Unknown"),
            ("reagent", reagentProto?.LocalizedName ?? "Unknown"));
        
        _chat.TrySendInGameICMessage(user, message, InGameICChatType.Speak, ChatTransmitRange.Normal);
    }

    /// <summary>
    /// Gets the primary reagent from a solution
    /// </summary>
    private bool TryGetMainReagent(Solution solution, out ReagentId reagentId, out ReagentPrototype? reagentProto)
    {
        reagentId = default;
        reagentProto = null;

        if (solution.Contents.Count == 0)
            return false;

        // Get the reagent with the highest quantity
        var mainReagent = solution.Contents.OrderByDescending(r => r.Quantity).First();
        reagentId = mainReagent.Reagent;

        return _prototypeManager.TryIndex(reagentId.Prototype, out reagentProto);
    }

    /// <summary>
    /// Resets announcement cooldown when reagent is switched
    /// </summary>
    public void ResetAnnouncementCooldown(EntityUid hypospray)
    {
        if (!TryComp<BorgHyposprayComponent>(hypospray, out var borgHypo))
            return;

        borgHypo.NextAnnouncementTime = TimeSpan.Zero;
        Dirty(hypospray, borgHypo);
    }
}
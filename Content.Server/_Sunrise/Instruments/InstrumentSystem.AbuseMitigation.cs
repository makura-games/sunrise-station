using System.Diagnostics.CodeAnalysis;
using Content.Server.Interaction;
using Content.Shared.ActionBlocker;
using Content.Shared.GameTicking;
using Content.Shared.Instruments;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;

#pragma warning disable IDE0130
namespace Content.Server.Instruments;

public sealed partial class InstrumentSystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private InteractionSystem _interaction = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    // Надеюсь у нас не будет раундов по 100000000 часов
    private readonly Dictionary<NetUserId, SessionMidiRateLimitData> _sessionMidiRateLimits = [];

    private void InitializeAbuse()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanUp);
    }

    private void OnCleanUp(RoundRestartCleanupEvent ev)
    {
        _sessionMidiRateLimits.Clear();
    }

    private bool TryValidateInstrumentRequest(
        NetEntity netUid,
        EntitySessionEventArgs args,
        out EntityUid uid,
        out EntityUid user,
        [NotNullWhen(true)] out InstrumentComponent? instrument,
        bool requireActiveInstrument = false,
        bool requirePlaying = false)
    {
        uid = EntityUid.Invalid;
        user = EntityUid.Invalid;
        instrument = null;

        var resolvedUid = GetEntity(netUid);
        if (!TryComp(resolvedUid, out instrument))
            return false;

        if (args.SenderSession.AttachedEntity is not { Valid: true } attached)
            return false;

        if (instrument.InstrumentPlayer != attached)
            return false;

        if (requireActiveInstrument && !HasComp<ActiveInstrumentComponent>(resolvedUid))
            return false;

        if (requirePlaying && !instrument.Playing)
            return false;

        if (!CanUseInstrument(attached, resolvedUid, instrument))
            return false;

        uid = resolvedUid;
        user = attached;
        return true;
    }

    private bool CanUseInstrument(EntityUid user, EntityUid uid, InstrumentComponent instrument)
    {
        if (user == uid)
            return true;

        if (instrument.Handheld
            && (!_container.TryGetContainingContainer((uid, null, null), out var container)
                || container.Owner != user))
        {
            return false;
        }

        if (!_actionBlocker.CanInteract(user, uid))
            return false;

        return _interaction.InRangeUnobstructed(user, uid);
    }

    private bool CanJoinInstrumentBand(EntityUid uid, EntityUid user, EntityUid master, InstrumentComponent masterInstrument)
    {
        if (uid == master
            || masterInstrument.Master != null
            || !HasComp<ActiveInstrumentComponent>(master))
        {
            return false;
        }

        if (masterInstrument.InstrumentPlayer is not { } masterPlayer)
            return false;

        return _examineSystem.InRangeUnOccluded(uid,
            master,
            MaxInstrumentBandRange,
            entity => entity == masterPlayer || entity == user);
    }

    private bool TryConsumeSessionMidiBudget(NetUserId userId, int eventCount)
    {
        var now = _timing.RealTime;

        if (!_sessionMidiRateLimits.TryGetValue(userId, out var state)
            || now >= state.WindowEnd)
        {
            state = new SessionMidiRateLimitData
            {
                WindowEnd = now.Add(TimeSpan.FromSeconds(1)),
                EventCount = 0,
                BatchCount = 0,
            };
        }

        state.EventCount += eventCount;
        state.BatchCount++;
        _sessionMidiRateLimits[userId] = state;

        return state.EventCount <= MaxMidiEventsPerSecond
               && state.BatchCount <= MaxMidiBatchesPerSecond;
    }

    private void RaiseInstrumentMidiEvent(EntityUid uid, InstrumentMidiEventEvent msg, EntityUid? excludedUser = null)
    {
        var filter = Filter.Pvs(uid, entityManager: EntityManager);

        if (excludedUser != null)
            filter.RemoveWhereAttachedEntity(entity => entity == excludedUser.Value);

        RaiseNetworkEvent(msg, filter);
    }

    private void RaiseInstrumentStopEvent(EntityUid uid, InstrumentStopMidiEvent msg, EntityUid? excludedUser = null)
    {
        var filter = Filter.Pvs(uid, entityManager: EntityManager);

        if (excludedUser != null)
            filter.RemoveWhereAttachedEntity(entity => entity == excludedUser.Value);

        RaiseNetworkEvent(msg, filter);
    }

    private struct SessionMidiRateLimitData
    {
        public TimeSpan WindowEnd;
        public long EventCount;
        public int BatchCount;
    }
}

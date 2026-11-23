using System.Diagnostics.CodeAnalysis;
using Content.Server.Silicons.Laws;
using Content.Shared._Sunrise.Silicons.StationAi;
using Content.Shared.Containers;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Silicons.StationAi;

public sealed partial class StationAiBodySystem
{
    /*
     * Features partial.
     *
     * This file owns extra gameplay features attached to station AI bodies:
     * radio channel inheritance from the AI core, and silicon law lookup
     * while the AI is operating through a body.
     */

    [Dependency] private SiliconLawSystem _siliconLaw = default!;

    #region Initialize

    /// <summary>
    /// Subscribes body feature handlers.
    /// </summary>
    private void InitializeBodyFeatures()
    {
        SubscribeLocalEvent<StationAiBodyComponent, GetSiliconLawsEvent>(OnBodyGetLaws);
    }

    #endregion

    #region Events

    /// <summary>
    /// Provides the linked station AI laws while the player is acting through a body.
    /// </summary>
    private void OnBodyGetLaws(Entity<StationAiBodyComponent> body, ref GetSiliconLawsEvent args)
    {
        if (args.Handled || body.Comp.LinkedAi is not { } stationAi)
            return;

        args.Laws = _siliconLaw.GetLaws(stationAi);
        args.Handled = true;
    }

    #endregion

    #region Radio

    /// <summary>
    /// Copies radio channels from the AI core holder to the controlled body.
    /// </summary>
    private void GrantStationAiRadioChannels(EntityUid stationAi, Entity<StationAiBodyComponent> body)
    {
        RevokeStationAiRadioChannels(body);

        if (!TryGetRadioChannelsHolderByAiCore(stationAi, out var radioChannelsHolder))
            return;

        if (TryComp<IntrinsicRadioTransmitterComponent>(body, out var transmitterReceiver)
            && TryComp<IntrinsicRadioTransmitterComponent>(radioChannelsHolder, out var transmitterTransmitter))
        {
            body.Comp.CachedRadioChannels[nameof(IntrinsicRadioTransmitterComponent)] = [..transmitterReceiver.Channels];

            transmitterReceiver.Channels.UnionWith(transmitterTransmitter.Channels);
            Dirty(body, transmitterReceiver);
        }

        if (TryComp<ActiveRadioComponent>(body, out var activeRadioReceiver)
            && TryComp<ActiveRadioComponent>(radioChannelsHolder, out var activeRadioTransmitter))
        {
            body.Comp.CachedRadioChannels[nameof(ActiveRadioComponent)] = [..activeRadioReceiver.Channels];

            activeRadioReceiver.Channels.UnionWith(activeRadioTransmitter.Channels);
            Dirty(body, activeRadioReceiver);
        }
    }

    /// <summary>
    /// Restores body radio channels that were cached before control was transferred.
    /// </summary>
    private void RevokeStationAiRadioChannels(Entity<StationAiBodyComponent> body)
    {
        if (body.Comp.CachedRadioChannels.Remove(nameof(IntrinsicRadioTransmitterComponent), out var transmitterChannels) &&
            TryComp<IntrinsicRadioTransmitterComponent>(body, out var transmitter))
        {
            transmitter.Channels = [..transmitterChannels];
            Dirty(body, transmitter);
        }

        if (body.Comp.CachedRadioChannels.Remove(nameof(ActiveRadioComponent), out var activeRadioChannels) &&
            TryComp<ActiveRadioComponent>(body, out var activeRadio))
        {
            activeRadio.Channels = [..activeRadioChannels];
            Dirty(body, activeRadio);
        }
    }

    /// <summary>
    /// Finds the entity inside the AI core that owns the radio channel set copied to bodies.
    /// </summary>
    private bool TryGetRadioChannelsHolderByAiCore(EntityUid stationAi, [NotNullWhen(true)] out EntityUid? radioChannelsHolder)
    {
        radioChannelsHolder = null;
        if (!TryComp<ContainerCompComponent>(stationAi, out var containerComp))
            return false;

        if (!_container.TryGetContainer(stationAi, containerComp.Container, out var container))
            return false;

        foreach (var containedEntity in container.ContainedEntities)
        {
            var proto = Prototype(containedEntity);
            if (proto == null || proto != containerComp.Proto)
                continue;

            radioChannelsHolder = containedEntity;
            return true;
        }

        return false;
    }

    #endregion
}

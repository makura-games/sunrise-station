using Content.Server.Salvage.Magnet;
using Content.Shared.Salvage.Magnet;

#pragma warning disable IDE0130
namespace Content.Server.Salvage;
#pragma warning restore IDE0130

public sealed partial class SalvageSystem
{
    private void InitializeAdvancedMagnet()
    {
        SubscribeLocalEvent<SalvageMagnetDataComponent, MagnetClaimOfferEvent>(OnAdvancedMagnetClaim);
        SubscribeLocalEvent<SalvageMagnetDataComponent, ComponentStartup>(OnAdvancedMagnetStartup);
        SubscribeLocalEvent<SalvageMagnetDataComponent, ComponentShutdown>(OnAdvancedMagnetShutdown);
        SubscribeLocalEvent<SalvageMagnetDataComponent, AnchorStateChangedEvent>(OnAdvancedMagnetAnchored);
    }

    private void OnAdvancedMagnetClaim(Entity<SalvageMagnetDataComponent> ent, ref MagnetClaimOfferEvent args)
    {
        if (!TryComp(ent.Owner, out SalvageMagnetComponent? magnet) ||
            ent.Comp.EndTime != null ||
            args.Index < 0 ||
            args.Index >= ent.Comp.Offered.Count)
            return;

        var index = args.Index;
        async void TryTakeMagnetOffer()
        {
            try
            {
                await TakeMagnetOffer(ent, index, (ent.Owner, magnet));
            }
            catch (Exception e)
            {
                _runtimeLog.LogException(e, $"{nameof(SalvageSystem)}.{nameof(TakeMagnetOffer)}");
            }
        }
        TryTakeMagnetOffer();
    }

    private void OnAdvancedMagnetStartup(Entity<SalvageMagnetDataComponent> ent, ref ComponentStartup args)
    {
        TryUpdateLocalMagnetUI(ent);
    }

    private void OnAdvancedMagnetShutdown(Entity<SalvageMagnetDataComponent> ent, ref ComponentShutdown args)
    {
        if (!HasComp<SalvageMagnetComponent>(ent.Owner) ||
            ent.Comp.ActiveEntities == null)
            return;

        EndMagnet(ent);
    }

    private void OnAdvancedMagnetAnchored(Entity<SalvageMagnetDataComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            return;

        TryUpdateLocalMagnetUI(ent);
    }

    private bool TryGetLocalMagnet(Entity<SalvageMagnetDataComponent> data, out Entity<SalvageMagnetComponent> magnet)
    {
        if (TryComp(data.Owner, out SalvageMagnetComponent? localMagnet))
        {
            magnet = (data.Owner, localMagnet);
            return true;
        }

        magnet = default;
        return false;
    }

    private bool TryUpdateLocalMagnetUI(Entity<SalvageMagnetDataComponent> data)
    {
        if (!HasComp<SalvageMagnetComponent>(data.Owner) ||
            TerminatingOrDeleted(data.Owner))
            return false;

        SetAdvancedMagnetUIState(data.Owner, data.Comp);
        return true;
    }

    private bool IsLocalMagnet(EntityUid magnetUid)
    {
        return HasComp<SalvageMagnetDataComponent>(magnetUid);
    }

    private void SetAdvancedMagnetUIState(EntityUid magnetUid, SalvageMagnetDataComponent data)
    {
        _ui.SetUiState(magnetUid, SalvageMagnetUiKey.Key,
            new SalvageMagnetBoundUserInterfaceState(data.Offered)
            {
                Cooldown = data.OfferCooldown,
                Duration = data.ActiveTime,
                EndTime = data.EndTime,
                NextOffer = data.NextOffer,
                ActiveSeed = data.ActiveSeed,
            });
    }
}

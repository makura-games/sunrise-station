using Content.Shared.Administration;
using Robust.Shared.Network;

namespace Content.Client.Administration.Systems;

public sealed partial class BwoinkSystem
{
    public event EventHandler<BwoinkCooldownMessage>? OnBwoinkCooldownReceived;

    public void LoadDbMessages(NetUserId userId)
    {
        RaiseNetworkEvent(new BwoinkRequestDbMessages(userId));
    }

    partial void InitializeSunrise()
    {
        SubscribeNetworkEvent<BwoinkCooldownMessage>(OnBwoinkCooldownMessage);
    }

    private void OnBwoinkCooldownMessage(BwoinkCooldownMessage message, EntitySessionEventArgs args)
    {
        OnBwoinkCooldownReceived?.Invoke(this, message);
    }
}

using System.Diagnostics.CodeAnalysis;
using Content.Shared.DeviceNetwork;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.DeviceNetwork;

/// <summary>
/// Типизированная оболочка для словарного протокола Sunrise Messenger.
/// </summary>
public sealed partial class SunriseNetworkPayload : Dictionary<string, object?>, INetworkPayload
{
    public bool TryGetValue<T>(string key, [NotNullWhen(true)] out T? value)
    {
        if (this.TryCastValue(key, out T? result))
        {
            value = result;
            return true;
        }

        value = default;
        return false;
    }
}

public static class SunriseDeviceNetworkConstants
{
    public const string Command = "command";
}

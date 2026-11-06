using System;
using System.Collections.Immutable;
using System.Net;
using Robust.Shared.Network;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public sealed partial class ServerDbPostgres
{
    private static void EnsureSunriseBanLookupKey(
        IPAddress? address,
        NetUserId? userId,
        ImmutableArray<byte>? hwId,
        ImmutableArray<ImmutableArray<byte>>? modernHWIds)
    {
        if (!HasBanLookupKey(address, userId, hwId, modernHWIds))
            throw new ArgumentException("Address, userId, hwId, and modernHWIds cannot all be empty");
    }

    internal static bool HasBanLookupKey(
        IPAddress? address,
        NetUserId? userId,
        ImmutableArray<byte>? hwId,
        ImmutableArray<ImmutableArray<byte>>? modernHWIds)
    {
        return address != null ||
               userId != null ||
               hwId is { Length: > 0 } ||
               modernHWIds is { Length: > 0 };
    }
}

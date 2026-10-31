// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/space-sunrise/space-station-14/blob/master/CLA.txt

using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.PlaytimeTop;

/// <summary>
/// Top player entry: username and total playtime.
/// </summary>
[Serializable, NetSerializable]
public sealed record PlaytimeTopEntry(string Username, TimeSpan TotalTime);

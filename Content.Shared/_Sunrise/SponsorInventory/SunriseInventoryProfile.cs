using System;
using System.Collections.Generic;
using System.Linq;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.SponsorInventory;

/// <summary>
/// Stores sponsor inventory choices for a humanoid profile.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class SunriseInventoryProfile : IEquatable<SunriseInventoryProfile>
{
    /// <summary>
    /// Base selection applied for every job before job-specific overrides.
    /// </summary>
    [DataField]
    public SunriseInventorySelection Global { get; set; } = new();

    /// <summary>
    /// Job-specific selections keyed by job prototype id. These are merged over <see cref="Global"/>.
    /// </summary>
    [DataField]
    public Dictionary<string, SunriseInventorySelection> Jobs { get; set; } = new();

    /// <summary>
    /// Creates a deep copy of this profile, including all global and job-specific selections.
    /// </summary>
    public SunriseInventoryProfile Clone()
    {
        var clone = new SunriseInventoryProfile
        {
            Global = Global?.Clone() ?? new SunriseInventorySelection(),
        };

        foreach (var (job, selection) in Jobs ?? new Dictionary<string, SunriseInventorySelection>())
        {
            if (selection != null)
                clone.Jobs[job] = selection.Clone();
        }

        return clone;
    }

    /// <summary>
    /// Converts the profile to the external sponsor API DTO.
    /// </summary>
    public SponsorInventoryProfileInfo ToInfo()
    {
        var info = new SponsorInventoryProfileInfo
        {
            Global = Global?.ToInfo() ?? new SponsorInventorySelectionInfo(),
        };

        foreach (var (job, selection) in Jobs ?? new Dictionary<string, SunriseInventorySelection>())
        {
            if (selection != null)
                info.Jobs[job] = selection.ToInfo();
        }

        return info;
    }

    /// <summary>
    /// Converts an external sponsor API DTO into the shared runtime profile format.
    /// </summary>
    public static SunriseInventoryProfile FromInfo(SponsorInventoryProfileInfo? info)
    {
        if (info == null)
            return new SunriseInventoryProfile();

        var profile = new SunriseInventoryProfile
        {
            Global = SunriseInventorySelection.FromInfo(info.Global),
        };

        foreach (var (job, selection) in info.Jobs ?? new Dictionary<string, SponsorInventorySelectionInfo>())
        {
            profile.Jobs[job] = SunriseInventorySelection.FromInfo(selection);
        }

        return profile;
    }

    /// <summary>
    /// Returns true when the profile contains no global or job-specific sponsor inventory selections.
    /// </summary>
    public bool IsEmpty()
    {
        var globalState = Global == null || Global.IsEmpty();
        var jobsState = Jobs == null || Jobs.Values.All(selection => selection == null || selection.IsEmpty());

        return globalState && jobsState;
    }

    /// <summary>
    /// Compares this profile with another profile by global selection and all job-specific selections.
    /// </summary>
    public bool Equals(SunriseInventoryProfile? other)
    {
        if (ReferenceEquals(null, other))
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (Global == null ||
            other.Global == null ||
            Jobs == null ||
            other.Jobs == null ||
            !Global.Equals(other.Global) ||
            Jobs.Count != other.Jobs.Count)
        {
            return false;
        }

        foreach (var (job, selection) in Jobs)
        {
            if (selection == null ||
                !other.Jobs.TryGetValue(job, out var otherSelection) ||
                otherSelection == null ||
                !selection.Equals(otherSelection))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares this profile with another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is SunriseInventoryProfile other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code based on the global selection and sorted job-specific selections.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Global);

        foreach (var (job, selection) in (Jobs ?? new Dictionary<string, SunriseInventorySelection>()).OrderBy(p => p.Key))
        {
            hash.Add(job);
            hash.Add(selection);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// Selected sponsor inventory items for one global or job-specific layer.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class SunriseInventorySelection : IEquatable<SunriseInventorySelection>
{
    /// <summary>
    /// Sponsor item ids selected for inventory equipment slots, keyed by slot id.
    /// </summary>
    [DataField]
    public Dictionary<string, string> SlotItems { get; set; } = new();

    /// <summary>
    /// Sponsor item ids selected for insertion into the character back storage preview.
    /// </summary>
    [DataField]
    public List<string> BagItems { get; set; } = new();

    public SunriseInventorySelection Clone()
    {
        return new SunriseInventorySelection
        {
            SlotItems = new Dictionary<string, string>(SlotItems ?? new Dictionary<string, string>()),
            BagItems = new List<string>(BagItems ?? new List<string>()),
        };
    }

    public SponsorInventorySelectionInfo ToInfo()
    {
        return new SponsorInventorySelectionInfo
        {
            SlotItems = new Dictionary<string, string>(SlotItems ?? new Dictionary<string, string>()),
            BagItems = new List<string>(BagItems ?? new List<string>()),
        };
    }

    public static SunriseInventorySelection FromInfo(SponsorInventorySelectionInfo? info)
    {
        if (info == null)
            return new SunriseInventorySelection();

        return new SunriseInventorySelection
        {
            SlotItems = new Dictionary<string, string>(info.SlotItems ?? new Dictionary<string, string>()),
            BagItems = new List<string>(info.BagItems ?? new List<string>()),
        };
    }

    public bool IsEmpty()
    {
        return (SlotItems == null || SlotItems.Count == 0) && (BagItems == null || BagItems.Count == 0);
    }

    public bool Equals(SunriseInventorySelection? other)
    {
        if (ReferenceEquals(null, other))
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (SlotItems == null ||
            other.SlotItems == null ||
            BagItems == null ||
            other.BagItems == null ||
            SlotItems.Count != other.SlotItems.Count ||
            !BagItems.SequenceEqual(other.BagItems))
        {
            return false;
        }

        foreach (var (slot, item) in SlotItems)
        {
            if (string.IsNullOrWhiteSpace(slot) ||
                string.IsNullOrWhiteSpace(item) ||
                !other.SlotItems.TryGetValue(slot, out var otherItem) ||
                otherItem != item)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is SunriseInventorySelection other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var (slot, item) in (SlotItems ?? new Dictionary<string, string>()).OrderBy(p => p.Key))
        {
            hash.Add(slot);
            hash.Add(item);
        }

        foreach (var item in BagItems ?? new List<string>())
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}

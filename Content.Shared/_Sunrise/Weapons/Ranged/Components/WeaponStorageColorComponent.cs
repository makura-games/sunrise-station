using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Weapons.Ranged.Components;

[RegisterComponent]
public sealed partial class WeaponStorageColorComponent : Component
{
    [DataField]
    public bool UseStorage;

    [DataField]
    public bool ColorMagazineStripe = true;

    [DataField]
    public bool SeparateBase = true;

    [DataField]
    public int MaxFill = 6;

    [DataField]
    public string? FullItemState;

    [DataField]
    public ProtoId<TagPrototype>? FullItemTag;
}

[Serializable, NetSerializable]
public enum WeaponStorageColorVisuals : byte
{
    ItemPrototype,
    // -1 обозначает предмет полного заполнения; остальные значения — количество предметов.
    Fill,
}

using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.SponsorSystem;

[Prototype]
public sealed partial class SponsorOocTitlePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("title", required: true)]
    public string Title { get; private set; } = default!;
}

[Prototype]
public sealed partial class SponsorOocColorPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Color? Color { get; private set; }

    [DataField]
    public List<Color> Colors { get; private set; } = new();
}

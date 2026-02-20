namespace BoardWC.Engine.Domain;

public readonly record struct ResourceBag(int Iron = 0, int Rice = 0, int Flower = 0)
{
    public static readonly ResourceBag Empty = new();

    public ResourceBag Add(ResourceType type, int amount) => type switch
    {
        ResourceType.Iron   => this with { Iron   = Iron   + amount },
        ResourceType.Rice   => this with { Rice   = Rice   + amount },
        ResourceType.Flower => this with { Flower = Flower + amount },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public ResourceBag Add(ResourceBag other) =>
        new(Iron + other.Iron, Rice + other.Rice, Flower + other.Flower);

    public ResourceBag Subtract(ResourceBag cost) =>
        new(Iron - cost.Iron, Rice - cost.Rice, Flower - cost.Flower);

    public bool CanAfford(ResourceBag cost) =>
        Iron >= cost.Iron && Rice >= cost.Rice && Flower >= cost.Flower;

    public int Total => Iron + Rice + Flower;

    public static ResourceBag operator +(ResourceBag a, ResourceBag b) => a.Add(b);

    public override string ToString() =>
        $"Iron:{Iron} Rice:{Rice} Flower:{Flower}";
}

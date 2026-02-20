namespace BoardWC.Engine.Domain;

public readonly record struct ResourceBag(int Food = 0, int Iron = 0, int ValueItem = 0)
{
    public static readonly ResourceBag Empty = new();

    public ResourceBag Add(ResourceType type, int amount) => type switch
    {
        ResourceType.Food      => this with { Food      = Food      + amount },
        ResourceType.Iron      => this with { Iron      = Iron      + amount },
        ResourceType.ValueItem => this with { ValueItem = ValueItem + amount },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public ResourceBag Add(ResourceBag other) =>
        new(Food + other.Food, Iron + other.Iron, ValueItem + other.ValueItem);

    public ResourceBag Subtract(ResourceBag cost) =>
        new(Food - cost.Food, Iron - cost.Iron, ValueItem - cost.ValueItem);

    public bool CanAfford(ResourceBag cost) =>
        Food >= cost.Food && Iron >= cost.Iron && ValueItem >= cost.ValueItem;

    public int Total => Food + Iron + ValueItem;

    public static ResourceBag operator +(ResourceBag a, ResourceBag b) => a.Add(b);

    public override string ToString() =>
        $"Food:{Food} Iron:{Iron} VI:{ValueItem}";
}

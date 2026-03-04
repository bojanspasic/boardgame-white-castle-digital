namespace BoardWC.Engine.Domain;

public readonly record struct ResourceBag(int Food = 0, int Iron = 0, int MotherOfPearls = 0)
{
    public static readonly ResourceBag Empty = new();

    public ResourceBag Add(ResourceType type, int amount) => type switch
    {
        ResourceType.Food      => this with { Food      = Food      + amount },
        ResourceType.Iron      => this with { Iron      = Iron      + amount },
        ResourceType.MotherOfPearls => this with { MotherOfPearls = MotherOfPearls + amount },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public ResourceBag Add(ResourceBag other) =>
        new(Food + other.Food, Iron + other.Iron, MotherOfPearls + other.MotherOfPearls);

    public ResourceBag Subtract(ResourceBag cost) =>
        new(Food - cost.Food, Iron - cost.Iron, MotherOfPearls - cost.MotherOfPearls);

    public bool CanAfford(ResourceBag cost) =>
        Food >= cost.Food && Iron >= cost.Iron && MotherOfPearls >= cost.MotherOfPearls;

    public int Total => Food + Iron + MotherOfPearls;

    public ResourceBag Clamp(int max) =>
        new(Math.Min(Food, max), Math.Min(Iron, max), Math.Min(MotherOfPearls, max));

    public static ResourceBag operator +(ResourceBag a, ResourceBag b) => a.Add(b);

    public override string ToString() =>
        $"Food:{Food} Iron:{Iron} Pearls:{MotherOfPearls}";
}

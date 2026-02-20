namespace BoardWC.Engine.Domain;

internal sealed class Bridge
{
    public BridgeColor Color { get; }

    internal Die? High   { get; private set; }
    internal List<Die> Middle { get; } = new();
    internal Die? Low    { get; private set; }

    public bool CanTakeFromHigh => High is not null;
    public bool CanTakeFromLow  => Low  is not null;

    public int DiceCount =>
        (High is not null ? 1 : 0) + Middle.Count + (Low is not null ? 1 : 0);

    public Bridge(BridgeColor color) => Color = color;

    public void RollAndArrange(int playerCount, Random rng)
    {
        High = null;
        Middle.Clear();
        Low = null;

        int count = DicePerRound(playerCount);
        var values = Enumerable.Range(0, count)
            .Select(_ => rng.Next(1, 7))
            .OrderByDescending(v => v)
            .ToList();

        if (count == 0) return;
        High = new Die(values[0], Color);
        if (count == 1) return;
        Low = new Die(values[^1], Color);
        for (int i = 1; i < count - 1; i++)
            Middle.Add(new Die(values[i], Color));
    }

    public Die? TakeFromHigh()
    {
        if (High is null) return null;
        var taken = High;
        if (Middle.Count > 0)
        {
            High = Middle[0];
            Middle.RemoveAt(0);
        }
        else
        {
            High = null;
        }
        return taken;
    }

    public Die? TakeFromLow()
    {
        if (Low is null) return null;
        var taken = Low;
        if (Middle.Count > 0)
        {
            Low = Middle[^1];
            Middle.RemoveAt(Middle.Count - 1);
        }
        else
        {
            Low = null;
        }
        return taken;
    }

    public BridgeSnapshot ToSnapshot() => new(
        Color,
        High?.ToSnapshot(),
        Middle.Select(d => d.ToSnapshot()).ToList().AsReadOnly(),
        Low?.ToSnapshot()
    );

    private static int DicePerRound(int playerCount) => playerCount switch
    {
        2 => 3,
        3 => 4,
        4 => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(playerCount),
            "White Castle supports 2-4 players.")
    };
}

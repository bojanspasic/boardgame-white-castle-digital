namespace BoardWC.Engine.Domain;

internal sealed class Die
{
    public int Value { get; }
    public BridgeColor Color { get; }

    public Die(int value, BridgeColor color)
    {
        Value = value;
        Color = color;
    }

    public DieSnapshot ToSnapshot() => new(Value, Color);

    public override string ToString() => $"{Color}:{Value}";
}

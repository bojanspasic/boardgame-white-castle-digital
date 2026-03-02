namespace BoardWC.Engine.Domain;

internal sealed record LanternChainGain(CardGainType Type, int Amount);

internal sealed class LanternChainItem
{
    public string SourceCardId   { get; init; } = "";
    public string SourceCardType { get; init; } = ""; // "ResourceSeed" | "ActionSeed" | "GroundFloor" | "MidFloor"
    public IReadOnlyList<LanternChainGain> Gains { get; init; } = [];

    public LanternChainItemSnapshot ToSnapshot() => new(
        SourceCardId, SourceCardType,
        Gains.Select(g => new LanternChainGainSnapshot(g.Type.ToString(), g.Amount))
             .ToList().AsReadOnly());
}

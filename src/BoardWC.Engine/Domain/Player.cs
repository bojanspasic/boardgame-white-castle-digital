namespace BoardWC.Engine.Domain;

internal sealed class Player
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public PlayerColor Color { get; init; }
    public bool IsAI { get; init; }

    internal ResourceBag Resources { get; set; }
    internal int LanternScore { get; set; }
    internal int Coins { get; set; }
    internal int MonarchialSeals { get; set; }
    internal int SoldiersAvailable { get; set; } = 5;
    internal int CourtiersAvailable { get; set; } = 5;
    internal int FarmersAvailable { get; set; } = 5;

    internal List<ClanCard> ClanCards { get; } = new();

    /// <summary>Dice the player has taken from bridges this round.</summary>
    internal List<Die> DiceInHand { get; } = new();

    public PlayerSnapshot ToSnapshot() => new(
        Id, Name, Color, IsAI,
        Resources, LanternScore, Coins,
        MonarchialSeals, SoldiersAvailable, CourtiersAvailable, FarmersAvailable,
        ClanCards.Select(c => c.ToSnapshot()).ToList().AsReadOnly(),
        DiceInHand.Select(d => d.ToSnapshot()).ToList().AsReadOnly()
    );
}

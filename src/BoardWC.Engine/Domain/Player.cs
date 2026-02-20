namespace BoardWC.Engine.Domain;

internal sealed class Player
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public PlayerColor Color { get; init; }
    public bool IsAI { get; init; }

    internal ResourceBag Resources { get; set; }
    internal int WorkersAvailable { get; set; }
    internal int WorkersOnBoard { get; set; }
    internal int LanternScore { get; set; }
    internal int Coins { get; set; }

    // Tower advancement: how many levels each zone has been advanced
    internal Dictionary<TowerZone, int> TowerLevels { get; } = new()
    {
        [TowerZone.Left]   = 0,
        [TowerZone.Center] = 0,
        [TowerZone.Right]  = 0,
    };

    internal List<ClanCard> ClanCards { get; } = new();

    /// <summary>Dice the player has taken from bridges this round.</summary>
    internal List<Die> DiceInHand { get; } = new();

    public PlayerSnapshot ToSnapshot() => new(
        Id, Name, Color, IsAI,
        Resources, WorkersAvailable, WorkersOnBoard, LanternScore, Coins,
        new TowerProgressSnapshot(
            TowerLevels[TowerZone.Left],
            TowerLevels[TowerZone.Center],
            TowerLevels[TowerZone.Right]),
        ClanCards.Select(c => c.ToSnapshot()).ToList().AsReadOnly(),
        DiceInHand.Select(d => d.ToSnapshot()).ToList().AsReadOnly()
    );
}

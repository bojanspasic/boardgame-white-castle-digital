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

    /// <summary>Number of unresolved AnyResource token choices from the well.</summary>
    internal int PendingAnyResourceChoices { get; set; }

    /// <summary>Remaining "place courtier at gate" uses from pending "Play castle" actions.</summary>
    internal int CastlePlaceRemaining { get; set; }

    /// <summary>Remaining "advance courtier" uses from pending "Play castle" actions.</summary>
    internal int CastleAdvanceRemaining { get; set; }

    internal int CourtiersAtGate { get; set; }
    internal int CourtiersOnGroundFloor { get; set; }
    internal int CourtiersOnMidFloor { get; set; }
    internal int CourtiersOnTopFloor { get; set; }

    internal List<ClanCard> ClanCards { get; } = new();

    /// <summary>Dice the player has taken from bridges this round.</summary>
    internal List<Die> DiceInHand { get; } = new();

    public PlayerSnapshot ToSnapshot() => new(
        Id, Name, Color, IsAI,
        Resources, LanternScore, Coins,
        MonarchialSeals, SoldiersAvailable, CourtiersAvailable, FarmersAvailable,
        PendingAnyResourceChoices, CastlePlaceRemaining, CastleAdvanceRemaining,
        CourtiersAtGate, CourtiersOnGroundFloor, CourtiersOnMidFloor, CourtiersOnTopFloor,
        ClanCards.Select(c => c.ToSnapshot()).ToList().AsReadOnly(),
        DiceInHand.Select(d => d.ToSnapshot()).ToList().AsReadOnly()
    );
}

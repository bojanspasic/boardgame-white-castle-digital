namespace BoardWC.Engine.Domain;

internal sealed class GameState
{
    public Guid GameId { get; } = Guid.NewGuid();
    public Phase CurrentPhase { get; internal set; } = Phase.Setup;
    public int CurrentRound { get; internal set; } = 1;
    public int MaxRounds { get; }
    public int ActivePlayerIndex { get; internal set; } = 0;

    internal List<Player> Players { get; }
    internal Board Board { get; }
    internal Random Rng { get; } = new();
    internal List<SeedCardPair> SeedCardPairs { get; } = new();

    /// <summary>Monotonically increasing counter; bumped each time any player actually gains influence.</summary>
    internal int InfluenceGainCounter { get; set; }

    public Player ActivePlayer => Players[ActivePlayerIndex];

    public GameState(List<Player> players, int maxRounds = 3)
    {
        Players   = players;
        MaxRounds = maxRounds;
        Board     = new Board();
    }

    /// <summary>Advance to the next player in rotation.</summary>
    public void AdvanceTurn() =>
        ActivePlayerIndex = (ActivePlayerIndex + 1) % Players.Count;

    public GameStateSnapshot ToSnapshot() => new(
        GameId,
        CurrentPhase,
        CurrentRound,
        MaxRounds,
        ActivePlayerIndex,
        Players.Select(p => p.ToSnapshot()).ToList().AsReadOnly(),
        Board.ToSnapshot(),
        SeedCardPairs.Select(p => p.ToSnapshot()).ToList().AsReadOnly()
    );
}

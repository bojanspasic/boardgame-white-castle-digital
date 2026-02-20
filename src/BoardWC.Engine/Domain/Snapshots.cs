namespace BoardWC.Engine.Domain;

// All snapshot types are public immutable records — the only data that crosses the engine boundary.

public sealed record GameStateSnapshot(
    Guid GameId,
    Phase CurrentPhase,
    int CurrentRound,
    int MaxRounds,
    int ActivePlayerIndex,
    IReadOnlyList<PlayerSnapshot> Players,
    BoardSnapshot Board,
    CardRowSnapshot ClanCardRow
);

public sealed record PlayerSnapshot(
    Guid Id,
    string Name,
    PlayerColor Color,
    bool IsAI,
    ResourceBag Resources,
    int WorkersAvailable,
    int WorkersOnBoard,
    int LanternScore,
    int Coins,
    TowerProgressSnapshot TowerProgress,
    IReadOnlyList<ClanCardSnapshot> ClanCards,
    IReadOnlyList<DieSnapshot> DiceInHand
);

public sealed record TowerProgressSnapshot(int LeftLevel, int CenterLevel, int RightLevel);

public sealed record BoardSnapshot(
    IReadOnlyList<BridgeSnapshot> Bridges,
    IReadOnlyList<TowerSnapshot> Towers,
    CastleSnapshot Castle,
    WellSnapshot Well,
    OutsideSnapshot Outside
)
{
    public int TotalDiceRemaining => Bridges.Sum(b =>
        (b.High != null ? 1 : 0) + b.Middle.Count + (b.Low != null ? 1 : 0));
}

public sealed record DieSnapshot(int Value, BridgeColor Color);

public sealed record BridgeSnapshot(
    BridgeColor Color,
    DieSnapshot? High,
    IReadOnlyList<DieSnapshot> Middle,
    DieSnapshot? Low
);

// ── Placement area snapshots ──────────────────────────────────────────────────

public sealed record DicePlaceholderSnapshot(
    int BaseValue,
    bool UnlimitedCapacity,
    IReadOnlyList<DieSnapshot> PlacedDice
);

public sealed record CastleSnapshot(
    IReadOnlyList<IReadOnlyList<DicePlaceholderSnapshot>> Floors
);

public sealed record WellSnapshot(DicePlaceholderSnapshot Placeholder);

public sealed record OutsideSnapshot(
    IReadOnlyList<DicePlaceholderSnapshot> Slots
);

// ── Tower snapshots ───────────────────────────────────────────────────────────

public sealed record TowerSnapshot(
    TowerZone Zone,
    IReadOnlyList<TowerLevelSnapshot> Levels
);

public sealed record TowerLevelSnapshot(
    int Level,
    TowerActionSnapshot Action,
    Guid? OccupiedBy
);

public sealed record TowerActionSnapshot(
    string Description,
    ResourceBag Cost,
    ResourceBag ResourceGain,
    int LanternsGained,
    TowerActionType ActionType
);

public sealed record ClanCardSnapshot(
    Guid CardId,
    string Name,
    string Effect,
    int VictoryPoints
);

public sealed record CardRowSnapshot(IReadOnlyList<ClanCardSnapshot> VisibleCards);

public sealed record PlayerScore(
    Guid PlayerId,
    string PlayerName,
    int Total,
    int LanternPoints,
    int ClanCardPoints,
    int TowerPoints
);

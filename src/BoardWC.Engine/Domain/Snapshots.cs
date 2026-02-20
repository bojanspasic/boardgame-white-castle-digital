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
    int LanternScore,
    int Coins,
    int MonarchialSeals,
    int SoldiersAvailable,
    int CourtiersAvailable,
    int FarmersAvailable,
    int PendingAnyResourceChoices,
    IReadOnlyList<ClanCardSnapshot> ClanCards,
    IReadOnlyList<DieSnapshot> DiceInHand
);

public sealed record BoardSnapshot(
    IReadOnlyList<BridgeSnapshot> Bridges,
    CastleSnapshot Castle,
    WellSnapshot Well,
    OutsideSnapshot Outside,
    int GroundFloorDeckRemaining,
    int MidFloorDeckRemaining
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

public sealed record TokenSnapshot(
    BridgeColor DieColor,
    TokenResource ResourceSide,
    bool IsResourceSideUp
);

public sealed record DicePlaceholderSnapshot(
    int BaseValue,
    bool UnlimitedCapacity,
    IReadOnlyList<DieSnapshot> PlacedDice,
    IReadOnlyList<TokenSnapshot> Tokens,
    RoomCardSnapshot? Card
);

public sealed record CastleSnapshot(
    IReadOnlyList<IReadOnlyList<DicePlaceholderSnapshot>> Floors
);

public sealed record WellSnapshot(DicePlaceholderSnapshot Placeholder);

public sealed record OutsideSnapshot(
    IReadOnlyList<DicePlaceholderSnapshot> Slots
);

// ── Room card snapshots ───────────────────────────────────────────────────────

public sealed record CardGainItemSnapshot(string GainType, int Amount);
public sealed record CardCostItemSnapshot(string CostType, int Amount);

public sealed record CardFieldSnapshot(
    bool IsGain,
    IReadOnlyList<CardGainItemSnapshot>? Gains,
    string? ActionDescription,
    IReadOnlyList<CardCostItemSnapshot>? ActionCost
);

public sealed record RoomCardSnapshot(
    string Id,
    string Name,
    IReadOnlyList<CardFieldSnapshot> Fields,
    string? Layout
);

// ── Clan card snapshots ───────────────────────────────────────────────────────

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
    int ClanCardPoints
);

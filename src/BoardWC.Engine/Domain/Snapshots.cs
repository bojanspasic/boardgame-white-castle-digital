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
    IReadOnlyList<SeedPairSnapshot> SeedPairs
);

public sealed record PlayerSnapshot(
    Guid Id,
    string Name,
    PlayerColor Color,
    bool IsAI,
    ResourceBag Resources,
    int LanternScore,
    int Influence,
    int Coins,
    int DaimyoSeals,
    int SoldiersAvailable,
    int CourtiersAvailable,
    int FarmersAvailable,
    int PendingAnyResourceChoices,
    int PendingTrainingGroundsActions,
    int PendingFarmActions,
    int CastlePlaceRemaining,
    int CastleAdvanceRemaining,
    int PendingOutsideActivationSlot,
    int PendingInfluenceGain,
    int PendingInfluenceSealCost,
    string? PendingCastleCardFieldFilter,
    bool PendingPersonalDomainRowChoice,
    RoomCardSnapshot? PendingNewCardActivation,
    int CourtiersAtGate,
    int CourtiersOnStewardFloor,
    int CourtiersOnDiplomatFloor,
    int CourtiersOnTopFloor,
    IReadOnlyList<DieSnapshot> DiceInHand,
    IReadOnlyList<PersonalDomainRowSnapshot> PersonalDomainRows,
    SeedActionCardSnapshot? SeedCard,
    IReadOnlyList<LanternChainItemSnapshot> LanternChain,
    IReadOnlyList<RoomCardSnapshot> PersonalDomainCards
);

public sealed record BoardSnapshot(
    IReadOnlyList<BridgeSnapshot> Bridges,
    CastleSnapshot Castle,
    WellSnapshot Well,
    OutsideSnapshot Outside,
    int StewardFloorDeckRemaining,
    int DiplomatFloorDeckRemaining,
    TrainingGroundsSnapshot TrainingGrounds,
    FarmingLandsSnapshot FarmingLands
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

// ── Top floor courtier room snapshots ────────────────────────────────────────

public sealed record TopFloorSlotSnapshot(
    int SlotIndex,
    IReadOnlyList<CardGainItemSnapshot> Gains,
    string? OccupantName   // null = empty
);

public sealed record TopFloorRoomSnapshot(string CardId, IReadOnlyList<TopFloorSlotSnapshot> Slots);

public sealed record CastleSnapshot(
    IReadOnlyList<IReadOnlyList<DicePlaceholderSnapshot>> Floors,
    TopFloorRoomSnapshot TopFloor
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

// ── Training grounds snapshots ────────────────────────────────────────────────

public sealed record TgAreaSnapshot(
    int AreaIndex,
    int IronCost,
    IReadOnlyList<CardGainItemSnapshot> ResourceGain,
    string ActionDescription,
    IReadOnlyList<string> SoldierOwners
);

public sealed record TrainingGroundsSnapshot(IReadOnlyList<TgAreaSnapshot> Areas);

// ── Farming lands snapshots ───────────────────────────────────────────────────

public sealed record FarmFieldSnapshot(
    BridgeColor BridgeColor,
    bool IsInland,
    int FoodCost,
    IReadOnlyList<CardGainItemSnapshot> GainItems,
    string ActionDescription,
    int VictoryPoints,
    IReadOnlyList<string> FarmerOwners
);

public sealed record FarmingLandsSnapshot(IReadOnlyList<FarmFieldSnapshot> Fields);

// ── Personal domain snapshots ─────────────────────────────────────────────────

public sealed record PersonalDomainSpotSnapshot(
    ResourceType GainType,
    int GainAmount,
    bool IsUncovered);

public sealed record PersonalDomainRowSnapshot(
    BridgeColor DieColor,
    int CompareValue,
    string FigureType,
    ResourceType DefaultGainType,
    int DefaultGainAmount,
    IReadOnlyList<PersonalDomainSpotSnapshot> Spots,
    DieSnapshot? PlacedDie);

// ── Lantern chain snapshots ───────────────────────────────────────────────────

public sealed record LanternChainGainSnapshot(string GainType, int Amount);
public sealed record LanternChainItemSnapshot(
    string SourceCardId,
    string SourceCardType,
    IReadOnlyList<LanternChainGainSnapshot> Gains);

// ── Seed card snapshots ───────────────────────────────────────────────────────

public sealed record SeedResourceGainSnapshot(string GainType, int Amount);
public sealed record SeedResourceCardSnapshot(
    string Id,
    IReadOnlyList<SeedResourceGainSnapshot> Gains,
    LanternChainGainSnapshot Back,
    string? DecreeCardId = null,
    LanternChainGainSnapshot? DecreeGain = null);
public sealed record SeedActionCardSnapshot(string Id, string ActionType, LanternChainGainSnapshot Back);
public sealed record SeedPairSnapshot(SeedActionCardSnapshot Action, SeedResourceCardSnapshot Resource);

public sealed record PlayerScore(
    Guid PlayerId,
    string PlayerName,
    int Total,
    int LanternPoints,
    int CourtierPoints,
    int CoinPoints,
    int SealPoints,
    int ResourcePoints,
    int FarmPoints,
    int TrainingGroundsPoints,
    int InfluencePoints
);

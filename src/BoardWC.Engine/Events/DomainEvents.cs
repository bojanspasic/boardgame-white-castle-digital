using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Events;

public sealed record GameStartedEvent(Guid GameId) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(GameStartedEvent);
}

public sealed record DieTakenFromBridgeEvent(
    Guid GameId,
    Guid PlayerId,
    BridgeColor BridgeColor,
    DiePosition Position,
    int DieValue
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(DieTakenFromBridgeEvent);
}

public sealed record LanternEffectFiredEvent(
    Guid GameId,
    Guid PlayerId
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(LanternEffectFiredEvent);
}

public sealed record DiePlacedEvent(
    Guid GameId,
    Guid PlayerId,
    PlacementTarget Target,
    int DieValue,
    int CoinDelta   // positive = earned, negative = spent
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(DiePlacedEvent);
}

public sealed record ResourcesCollectedEvent(
    Guid GameId,
    Guid PlayerId,
    ResourceBag Gained
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(ResourcesCollectedEvent);
}

public sealed record LanternsGainedEvent(
    Guid GameId,
    Guid PlayerId,
    int Amount
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(LanternsGainedEvent);
}

public sealed record PlayerPassedEvent(
    Guid GameId,
    Guid PlayerId
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(PlayerPassedEvent);
}

public sealed record RoundEndedEvent(
    Guid GameId,
    int RoundNumber
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(RoundEndedEvent);
}

public sealed record GameOverEvent(
    Guid GameId,
    IReadOnlyList<PlayerScore> FinalScores
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(GameOverEvent);
}

public sealed record WellEffectAppliedEvent(
    Guid GameId,
    Guid PlayerId,
    int SealGained,
    ResourceBag ResourcesGained,
    int CoinsGained,
    int PendingChoices   // count of AnyResource tokens requiring a choice
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(WellEffectAppliedEvent);
}

public sealed record AnyResourceChosenEvent(
    Guid GameId,
    Guid PlayerId,
    ResourceType Choice
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(AnyResourceChosenEvent);
}

public sealed record CardFieldGainActivatedEvent(
    Guid GameId,
    Guid PlayerId,
    string CardId,
    int FieldIndex,
    ResourceBag ResourcesGained,
    int CoinsGained,
    int SealsGained,
    int LanternGained,
    int VpGained,
    int InfluenceGained
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(CardFieldGainActivatedEvent);
}

public sealed record CardActionActivatedEvent(
    Guid GameId,
    Guid PlayerId,
    string CardId,
    int FieldIndex,
    string ActionDescription
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(CardActionActivatedEvent);
}

public sealed record TrainingGroundsUsedEvent(
    Guid GameId,
    Guid PlayerId,
    int AreaIndex,           // -1 = skipped
    int IronSpent,
    ResourceBag ResourcesGained,
    int CoinsGained,
    int SealsGained,
    int LanternGained,
    string? ActionTriggered  // null if skipped or area has no action side
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(TrainingGroundsUsedEvent);
}

public sealed record FarmerPlacedEvent(
    Guid GameId,
    Guid PlayerId,
    BridgeColor BridgeColor,
    bool IsInland,            // false and BridgeColor ignored when WasSkipped
    bool WasSkipped,          // true = player skipped the farm action
    int FoodSpent,
    ResourceBag ResourcesGained,
    int CoinsGained,
    int SealsGained,
    int LanternGained,
    string? ActionTriggered   // null if skipped or gain-only card
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(FarmerPlacedEvent);
}

public sealed record FarmEffectFiredEvent(
    Guid GameId,
    Guid PlayerId,
    BridgeColor BridgeColor,
    bool IsInland,
    ResourceBag ResourcesGained,
    int CoinsGained,
    int SealsGained,
    int LanternGained,
    string? ActionTriggered
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(FarmEffectFiredEvent);
}

public sealed record CastlePlayExecutedEvent(
    Guid GameId,
    Guid PlayerId,
    bool PlacedAtGate,
    CourtierPosition? AdvancedFrom,
    int LevelsAdvanced
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(CastlePlayExecutedEvent);
}

public sealed record TopFloorSlotFilledEvent(
    Guid GameId,
    Guid PlayerId,
    int SlotIndex,
    ResourceBag ResourcesGained,
    int CoinsGained,
    int SealsGained,
    int LanternGained
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(TopFloorSlotFilledEvent);
}

public sealed record OutsideActivationChosenEvent(
    Guid GameId,
    Guid PlayerId,
    int SlotIndex,
    OutsideActivation Choice
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(OutsideActivationChosenEvent);
}

public sealed record PersonalDomainActivatedEvent(
    Guid GameId,
    Guid PlayerId,
    int RowIndex,
    BridgeColor DieColor,
    int UncoveredSpots,
    ResourceBag ResourcesGained
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(PersonalDomainActivatedEvent);
}

public sealed record SeedPairChosenEvent(
    Guid GameId,
    Guid PlayerId,
    string ActionCardId,
    string ActionType,
    ResourceBag ResourcesGained,
    int CoinsGained,
    int SealsGained,
    int PendingAnyChoices
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(SeedPairChosenEvent);
}

public sealed record SeedCardActivatedEvent(
    Guid GameId,
    Guid PlayerId,
    string ActionCardId,
    string ActionType,
    int RowIndex
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(SeedCardActivatedEvent);
}

public sealed record LanternChainItemAddedEvent(
    Guid GameId,
    Guid PlayerId,
    string SourceCardId,
    string SourceCardType,
    IReadOnlyList<(string GainType, int Amount)> Gains
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(LanternChainItemAddedEvent);
}

public sealed record LanternChainActivatedEvent(
    Guid GameId,
    Guid PlayerId,
    ResourceBag Resources,
    int Coins,
    int Seals,
    int VpGained
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(LanternChainActivatedEvent);
}

public sealed record RoomCardAcquiredEvent(
    Guid GameId,
    Guid PlayerId,
    string CardId,
    string CardName,
    int Floor   // 0 = steward floor, 1 = diplomat floor
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(RoomCardAcquiredEvent);
}

public sealed record PersonalDomainCardFieldActivatedEvent(
    Guid GameId,
    Guid PlayerId,
    string CardId,
    int FieldIndex,
    ResourceBag ResourcesGained,
    int CoinsGained,
    int SealsGained,
    int LanternGained,
    int VpGained,
    int InfluenceGained
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(PersonalDomainCardFieldActivatedEvent);
}

/// <summary>
/// Fired when an influence gain crosses a threshold (5, 10, or 15) and the player
/// must decide whether to pay the Daimyo Seal cost.
/// </summary>
public sealed record InfluenceGainPendingEvent(
    Guid GameId,
    Guid PlayerId,
    int InfluenceGain,
    int SealCost
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(InfluenceGainPendingEvent);
}

/// <summary>Fired when the player resolves a pending influence gain (accepts or refuses).</summary>
public sealed record InfluenceGainResolvedEvent(
    Guid GameId,
    Guid PlayerId,
    int InfluenceGain,
    int SealsPaid,
    bool Accepted
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(InfluenceGainResolvedEvent);
}

/// <summary>Fired when the player resolves a pending castle card field choice (or skips).</summary>
public sealed record CastleCardFieldChosenEvent(
    Guid GameId,
    Guid PlayerId,
    int Floor,              // -1 = skipped
    int RoomIndex,
    int FieldIndex,
    ResourceBag ResourcesGained,
    int CoinsGained,
    int SealsGained,
    int LanternGained,
    int VpGained,
    int InfluenceGained,
    string? ActionTriggered  // null when skipped or gain field
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(CastleCardFieldChosenEvent);
}

/// <summary>Fired when the player activates a personal domain row for free (no die placed).</summary>
public sealed record PersonalDomainRowChosenEvent(
    Guid GameId,
    Guid PlayerId,
    BridgeColor RowColor,
    ResourceBag ResourcesGained
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(PersonalDomainRowChosenEvent);
}

/// <summary>Fired when the player resolves a pending new-card field choice (or skips).</summary>
public sealed record NewCardFieldChosenEvent(
    Guid GameId,
    Guid PlayerId,
    string CardId,
    int FieldIndex,          // -1 = skipped
    ResourceBag ResourcesGained,
    int CoinsGained,
    int SealsGained,
    int LanternGained,
    int VpGained,
    int InfluenceGained,
    string? ActionTriggered  // null when skipped or gain field
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(NewCardFieldChosenEvent);
}

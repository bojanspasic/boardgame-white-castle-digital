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
    int LanternGained
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
    bool IsInland,            // false and BridgeColor ignored when skipped (AreaIndex == -1)
    int AreaIndex,            // -1 = skipped
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

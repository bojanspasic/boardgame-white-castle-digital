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

public sealed record ClanCardAcquiredEvent(
    Guid GameId,
    Guid PlayerId,
    ClanCardSnapshot Card
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(ClanCardAcquiredEvent);
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

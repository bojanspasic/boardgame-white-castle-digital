namespace BoardWC.Engine.Events;

public interface IDomainEvent
{
    Guid GameId { get; }
    DateTimeOffset OccurredAt { get; }
    string EventType { get; }
}

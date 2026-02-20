using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Engine;

public abstract record ActionResult
{
    private ActionResult() { }

    public sealed record Success(
        GameStateSnapshot NewState,
        IReadOnlyList<IDomainEvent> Events
    ) : ActionResult;

    public sealed record Failure(
        GameStateSnapshot CurrentState,
        string Reason
    ) : ActionResult;
}

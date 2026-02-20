using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class CompositeActionHandler : IActionHandler
{
    private readonly IReadOnlyList<IActionHandler> _handlers;

    public CompositeActionHandler(IReadOnlyList<IActionHandler> handlers)
        => _handlers = handlers;

    public bool CanHandle(IGameAction action) =>
        _handlers.Any(h => h.CanHandle(action));

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var handler = Find(action);
        return handler?.Validate(action, state)
               ?? ValidationResult.Fail($"No handler for action type '{action.GetType().Name}'.");
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var handler = Find(action)
            ?? throw new InvalidOperationException(
                $"No handler for '{action.GetType().Name}'. Always validate before applying.");
        handler.Apply(action, state, events);
    }

    private IActionHandler? Find(IGameAction action) =>
        _handlers.FirstOrDefault(h => h.CanHandle(action));
}

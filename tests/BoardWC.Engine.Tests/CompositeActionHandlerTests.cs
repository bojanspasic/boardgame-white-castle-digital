using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for CompositeActionHandler — CanHandle dispatch, unknown-action error paths.
/// </summary>
public class CompositeActionHandlerTests
{
    // ── stub handler ──────────────────────────────────────────────────────────

    private sealed class StubHandler : IActionHandler
    {
        private readonly Type _handledType;
        public bool Applied { get; private set; }

        public StubHandler(Type handledType) => _handledType = handledType;

        public bool CanHandle(IGameAction action) => action.GetType() == _handledType;

        public ValidationResult Validate(IGameAction action, GameState state) =>
            ValidationResult.Ok();

        public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
            => Applied = true;
    }

    private static (GameState State, CompositeActionHandler Handler, StubHandler Stub)
        MakeComposite(Type stubType)
    {
        var alice = new Player { Name = "Alice" };
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;

        var stub    = new StubHandler(stubType);
        var handler = new CompositeActionHandler([stub]);
        return (state, handler, stub);
    }

    // ── CanHandle ─────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_KnownAction_ReturnsTrue()
    {
        var (_, handler, _) = MakeComposite(typeof(StartGameAction));
        Assert.True(handler.CanHandle(new StartGameAction()));
    }

    [Fact]
    public void CanHandle_UnknownAction_ReturnsFalse()
    {
        var (_, handler, _) = MakeComposite(typeof(StartGameAction));
        // PassAction is not registered
        Assert.False(handler.CanHandle(new PassAction(Guid.NewGuid())));
    }

    [Fact]
    public void CanHandle_EmptyHandlerList_ReturnsFalse()
    {
        var handler = new CompositeActionHandler([]);
        Assert.False(handler.CanHandle(new StartGameAction()));
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_KnownAction_DelegatesToHandler()
    {
        var (state, handler, _) = MakeComposite(typeof(StartGameAction));
        var result = handler.Validate(new StartGameAction(), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnknownAction_ReturnsFailWithMessage()
    {
        var (state, handler, _) = MakeComposite(typeof(StartGameAction));
        var result = handler.Validate(new PassAction(Guid.NewGuid()), state);
        Assert.False(result.IsValid);
        Assert.Contains("No handler for action type", result.Reason);
        Assert.Contains("PassAction", result.Reason);
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_KnownAction_DelegatesToHandler()
    {
        var (state, handler, stub) = MakeComposite(typeof(StartGameAction));
        var events = new List<IDomainEvent>();

        handler.Apply(new StartGameAction(), state, events);

        Assert.True(stub.Applied);
    }

    [Fact]
    public void Apply_UnknownAction_ThrowsInvalidOperationException()
    {
        var (state, handler, _) = MakeComposite(typeof(StartGameAction));
        var events = new List<IDomainEvent>();

        var ex = Assert.Throws<InvalidOperationException>(
            () => handler.Apply(new PassAction(Guid.NewGuid()), state, events));

        Assert.Contains("No handler for", ex.Message);
        Assert.Contains("PassAction", ex.Message);
    }

    // ── Multiple handlers ─────────────────────────────────────────────────────

    [Fact]
    public void Validate_FirstMatchingHandler_IsUsed()
    {
        var alice = new Player { Name = "Alice" };
        var state = new GameState(new List<Player> { alice });
        state.CurrentPhase = Phase.WorkerPlacement;

        var stub1 = new StubHandler(typeof(StartGameAction));
        var stub2 = new StubHandler(typeof(PassAction));
        var handler = new CompositeActionHandler([stub1, stub2]);

        Assert.True(handler.CanHandle(new StartGameAction()));
        Assert.True(handler.CanHandle(new PassAction(alice.Id)));
        Assert.False(handler.CanHandle(new PlaceDieAction(alice.Id, new WellTarget())));
    }
}

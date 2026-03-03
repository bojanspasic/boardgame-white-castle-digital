using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for PassHandler — the three Validate failure branches.
/// </summary>
public class PassHandlerTests
{
    private static (Player Alice, Player Bob, GameState State, PassHandler Handler)
        MakeState(Action<GameState>? configureState = null)
    {
        var alice = new Player { Name = "Alice" };
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        configureState?.Invoke(state);
        return (alice, bob, state, new PassHandler());
    }

    // ── Validation failures ───────────────────────────────────────────────────

    [Fact]
    public void Validate_WrongPhase_Fails()
    {
        var (alice, _, state, handler) = MakeState(s => s.CurrentPhase = Phase.SeedCardSelection);

        var result = handler.Validate(new PassAction(alice.Id), state);

        Assert.False(result.IsValid);
        Assert.Contains("worker placement", result.Reason);
    }

    [Fact]
    public void Validate_UnknownPlayer_Fails()
    {
        var (_, _, state, handler) = MakeState();
        var unknownId = Guid.NewGuid();

        var result = handler.Validate(new PassAction(unknownId), state);

        Assert.False(result.IsValid);
        Assert.Contains("Unknown player", result.Reason);
    }

    [Fact]
    public void Validate_WrongPlayersTurn_Fails()
    {
        var (alice, bob, state, handler) = MakeState();
        // Alice is active (index 0); Bob tries to pass
        var result = handler.Validate(new PassAction(bob.Id), state);

        Assert.False(result.IsValid);
        Assert.Contains("not this player's turn", result.Reason);
    }

    [Fact]
    public void Validate_ValidPass_Succeeds()
    {
        var (alice, _, state, handler) = MakeState();

        var result = handler.Validate(new PassAction(alice.Id), state);

        Assert.True(result.IsValid);
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_EmitsPlayerPassedEvent()
    {
        var (alice, _, state, handler) = MakeState();
        var events = new List<IDomainEvent>();

        handler.Apply(new PassAction(alice.Id), state, events);

        var evt = Assert.Single(events.OfType<PlayerPassedEvent>());
        Assert.Equal(state.GameId, evt.GameId);
        Assert.Equal(alice.Id,     evt.PlayerId);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(PlayerPassedEvent), evt.EventType);
    }
}

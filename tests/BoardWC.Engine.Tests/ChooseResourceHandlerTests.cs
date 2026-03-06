using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for ChooseResourceHandler — pending choice decrement, resource gain, and clamping.
/// </summary>
public class ChooseResourceHandlerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static (Player Alice, Player Bob, GameState State, ChooseResourceHandler Handler) MakeState(
        Action<Player>? setupAlice = null)
    {
        var alice = new Player { Name = "Alice" };
        setupAlice?.Invoke(alice);
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        return (alice, bob, state, new ChooseResourceHandler());
    }

    // ── CanHandle ─────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_ChooseResourceAction_ReturnsTrue()
    {
        var handler = new ChooseResourceHandler();
        Assert.True(handler.CanHandle(new ChooseResourceAction(Guid.NewGuid(), ResourceType.Food)));
    }

    [Fact]
    public void CanHandle_OtherAction_ReturnsFalse()
    {
        var handler = new ChooseResourceHandler();
        Assert.False(handler.CanHandle(new StartGameAction()));
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_UnknownPlayer_Fails()
    {
        var (_, _, state, handler) = MakeState();
        var result = handler.Validate(new ChooseResourceAction(Guid.NewGuid(), ResourceType.Food), state);
        Assert.False(result.IsValid);
        Assert.Contains("Unknown player", result.Reason);
    }

    [Fact]
    public void Validate_NotActivePlayer_Fails()
    {
        var (_, bob, state, handler) = MakeState();
        bob.PendingAnyResourceChoices = 1;
        var result = handler.Validate(new ChooseResourceAction(bob.Id, ResourceType.Food), state);
        Assert.False(result.IsValid);
        Assert.Contains("not this player's turn", result.Reason);
    }

    [Fact]
    public void Validate_NoPendingChoices_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        alice.PendingAnyResourceChoices = 0;
        var result = handler.Validate(new ChooseResourceAction(alice.Id, ResourceType.Food), state);
        Assert.False(result.IsValid);
        Assert.Contains("No pending resource choice", result.Reason);
    }

    [Fact]
    public void Validate_WithPendingChoice_Succeeds()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingAnyResourceChoices = 1);
        var result = handler.Validate(new ChooseResourceAction(alice.Id, ResourceType.Iron), state);
        Assert.True(result.IsValid);
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_DecrementsPendingChoices()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingAnyResourceChoices = 2);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseResourceAction(alice.Id, ResourceType.Food), state, events);

        Assert.Equal(1, alice.PendingAnyResourceChoices);
    }

    [Fact]
    public void Apply_GrantsChosenResource_Food()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingAnyResourceChoices = 1);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseResourceAction(alice.Id, ResourceType.Food), state, events);

        Assert.Equal(1, alice.Resources.Food);
        Assert.Equal(0, alice.Resources.Iron);
        Assert.Equal(0, alice.Resources.MotherOfPearls);
    }

    [Fact]
    public void Apply_GrantsChosenResource_Iron()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingAnyResourceChoices = 1);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseResourceAction(alice.Id, ResourceType.Iron), state, events);

        Assert.Equal(1, alice.Resources.Iron);
    }

    [Fact]
    public void Apply_GrantsChosenResource_MotherOfPearls()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingAnyResourceChoices = 1);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseResourceAction(alice.Id, ResourceType.MotherOfPearls), state, events);

        Assert.Equal(1, alice.Resources.MotherOfPearls);
    }

    [Fact]
    public void Apply_ClampsResourceAt7()
    {
        var (alice, _, state, handler) = MakeState(a =>
        {
            // Already at 7 food
            a.Resources = new ResourceBag(7, 0, 0);
            a.PendingAnyResourceChoices = 1;
        });
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseResourceAction(alice.Id, ResourceType.Food), state, events);

        // Should not exceed 7
        Assert.Equal(7, alice.Resources.Food);
    }

    [Fact]
    public void Apply_EmitsAnyResourceChosenEvent()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingAnyResourceChoices = 1);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseResourceAction(alice.Id, ResourceType.MotherOfPearls), state, events);

        var evt = Assert.Single(events.OfType<AnyResourceChosenEvent>());
        Assert.Equal(alice.Id, evt.PlayerId);
        Assert.Equal(ResourceType.MotherOfPearls, evt.Choice);
    }

    [Fact]
    public void Apply_MultiplePendingChoices_RequiresMultipleCalls()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingAnyResourceChoices = 3);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseResourceAction(alice.Id, ResourceType.Food), state, events);
        Assert.Equal(2, alice.PendingAnyResourceChoices);

        handler.Apply(new ChooseResourceAction(alice.Id, ResourceType.Iron), state, events);
        Assert.Equal(1, alice.PendingAnyResourceChoices);

        handler.Apply(new ChooseResourceAction(alice.Id, ResourceType.MotherOfPearls), state, events);
        Assert.Equal(0, alice.PendingAnyResourceChoices);

        Assert.Equal(1, alice.Resources.Food);
        Assert.Equal(1, alice.Resources.Iron);
        Assert.Equal(1, alice.Resources.MotherOfPearls);
    }
}

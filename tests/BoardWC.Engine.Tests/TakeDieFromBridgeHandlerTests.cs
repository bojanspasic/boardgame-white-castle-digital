using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for TakeDieFromBridgeHandler — validation edge cases and Low-position
/// lantern trigger.
/// </summary>
public class TakeDieFromBridgeHandlerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static (Player Alice, Player Bob, GameState State, TakeDieFromBridgeHandler Handler) MakeState()
    {
        var alice = new Player { Name = "Alice" };
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        // Manually set up the Red bridge with a High and Low die
        var bridge = state.Board.GetBridge(BridgeColor.Red);
        bridge.RollAndArrange(2, new Random(42));
        return (alice, bob, state, new TakeDieFromBridgeHandler());
    }

    // ── CanHandle ─────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_TakeDieFromBridgeAction_ReturnsTrue()
    {
        var handler = new TakeDieFromBridgeHandler();
        Assert.True(handler.CanHandle(new TakeDieFromBridgeAction(Guid.NewGuid(), BridgeColor.Red, DiePosition.High)));
    }

    [Fact]
    public void CanHandle_OtherAction_ReturnsFalse()
    {
        var handler = new TakeDieFromBridgeHandler();
        Assert.False(handler.CanHandle(new StartGameAction()));
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WrongPhase_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        state.CurrentPhase = Phase.Setup;
        var result = handler.Validate(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High), state);
        Assert.False(result.IsValid);
        Assert.Contains("worker placement", result.Reason);
    }

    [Fact]
    public void Validate_UnknownPlayer_Fails()
    {
        var (_, _, state, handler) = MakeState();
        var result = handler.Validate(new TakeDieFromBridgeAction(Guid.NewGuid(), BridgeColor.Red, DiePosition.High), state);
        Assert.False(result.IsValid);
        Assert.Contains("Unknown player", result.Reason);
    }

    [Fact]
    public void Validate_NotActivePlayer_Fails()
    {
        var (_, bob, state, handler) = MakeState();
        // Alice is active (index 0), Bob tries to act
        var result = handler.Validate(new TakeDieFromBridgeAction(bob.Id, BridgeColor.Red, DiePosition.High), state);
        Assert.False(result.IsValid);
        Assert.Contains("not this player's turn", result.Reason);
    }

    [Fact]
    public void Validate_EmptyHighPosition_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        var bridge = state.Board.GetBridge(BridgeColor.Red);
        // Drain all dice
        while (bridge.CanTakeFromHigh) bridge.TakeFromHigh();
        while (bridge.CanTakeFromLow)  bridge.TakeFromLow();

        var result = handler.Validate(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High), state);
        Assert.False(result.IsValid);
        Assert.Contains("High", result.Reason);
    }

    [Fact]
    public void Validate_EmptyLowPosition_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        var bridge = state.Board.GetBridge(BridgeColor.Red);
        // A 2-player bridge gets 3 dice: High, Middle, Low.
        // Drain all dice from Low side until CanTakeFromLow is false.
        while (bridge.CanTakeFromLow) bridge.TakeFromLow();
        while (bridge.CanTakeFromHigh) bridge.TakeFromHigh();

        var result = handler.Validate(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.Low), state);
        Assert.False(result.IsValid);
        Assert.Contains("Low", result.Reason);
    }

    [Fact]
    public void Validate_ValidHighTake_Succeeds()
    {
        var (alice, _, state, handler) = MakeState();
        var result = handler.Validate(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ValidLowTake_Succeeds()
    {
        var (alice, _, state, handler) = MakeState();
        var result = handler.Validate(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.Low), state);
        Assert.True(result.IsValid);
    }

    // ── Apply — High position (no lantern) ───────────────────────────────────

    [Fact]
    public void Apply_HighPosition_DieMovedToHand_NoLanternEvent()
    {
        var (alice, _, state, handler) = MakeState();
        var events = new List<IDomainEvent>();

        handler.Apply(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High), state, events);

        Assert.Single(alice.DiceInHand);
        Assert.DoesNotContain(events, e => e is LanternEffectFiredEvent);
        Assert.Contains(events, e => e is DieTakenFromBridgeEvent taken
            && taken.BridgeColor == BridgeColor.Red
            && taken.Position == DiePosition.High);
    }

    // ── Apply — Low position (lantern fires) ──────────────────────────────────

    [Fact]
    public void Apply_LowPosition_FiresLanternEffectEvent()
    {
        var (alice, _, state, handler) = MakeState();
        var events = new List<IDomainEvent>();

        handler.Apply(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.Low), state, events);

        Assert.Single(alice.DiceInHand);
        Assert.Contains(events, e => e is LanternEffectFiredEvent lef && lef.PlayerId == alice.Id);
    }

    [Fact]
    public void Apply_LowPosition_EmptyLanternChain_OnlyFiredEvent()
    {
        var (alice, _, state, handler) = MakeState();
        var events = new List<IDomainEvent>();
        // No chain items — only the LanternEffectFiredEvent should appear, no ResourceGainedEvents
        handler.Apply(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.Low), state, events);

        // DieTakenFromBridgeEvent + LanternEffectFiredEvent only
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void Apply_LowPosition_WithChainItem_GrantsResources()
    {
        var (alice, _, state, handler) = MakeState();
        // Add a lantern chain item that gives 2 Food
        alice.LanternChain.Add(new LanternChainItem
        {
            SourceCardId   = "card-1",
            SourceCardType = "StewardFloor",
            Gains          = [new LanternChainGain(CardGainType.Food, 2)]
        });

        var events = new List<IDomainEvent>();
        handler.Apply(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.Low), state, events);

        Assert.Equal(2, alice.Resources.Food);
        Assert.Contains(events, e => e is LanternChainActivatedEvent lca && lca.Resources.Food == 2);
    }

    [Fact]
    public void Apply_DieTakenEvent_ContainsBridgeColorAndValue()
    {
        var (alice, _, state, handler) = MakeState();
        var bridge = state.Board.GetBridge(BridgeColor.Red);
        // Peek at the High die value before taking it
        int highValue = bridge.High!.Value;

        var events = new List<IDomainEvent>();
        handler.Apply(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High), state, events);

        var taken = Assert.Single(events.OfType<DieTakenFromBridgeEvent>());
        Assert.Equal(BridgeColor.Red, taken.BridgeColor);
        Assert.Equal(DiePosition.High, taken.Position);
        Assert.Equal(highValue, taken.DieValue);
    }
}

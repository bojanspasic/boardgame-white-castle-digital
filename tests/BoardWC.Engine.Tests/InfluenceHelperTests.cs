using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for InfluenceHelper — SealCost calculation and Apply behaviour.
/// </summary>
public class InfluenceHelperTests
{
    // ── SealCost ─────────────────────────────────────────────────────────────

    [Fact]
    public void SealCost_NoThresholdCrossed_ReturnsZero()
    {
        Assert.Equal(0, InfluenceHelper.SealCost(0, 4));
        Assert.Equal(0, InfluenceHelper.SealCost(2, 4));
        Assert.Equal(0, InfluenceHelper.SealCost(6, 9));
        Assert.Equal(0, InfluenceHelper.SealCost(11, 14));
    }

    [Fact]
    public void SealCost_CrossingFiveThreshold_CostsOneSeal()
    {
        Assert.Equal(1, InfluenceHelper.SealCost(4, 5));
        Assert.Equal(1, InfluenceHelper.SealCost(0, 5));
        Assert.Equal(1, InfluenceHelper.SealCost(3, 7));
    }

    [Fact]
    public void SealCost_CrossingTenThreshold_CostsTwoAdditionalSeals()
    {
        // Already past 5 → only crosses 10 threshold
        Assert.Equal(2, InfluenceHelper.SealCost(5, 10));
        Assert.Equal(2, InfluenceHelper.SealCost(7, 12));
    }

    [Fact]
    public void SealCost_CrossingFifteenThreshold_CostsThreeAdditionalSeals()
    {
        // Already past 10 → only crosses 15 threshold
        Assert.Equal(3, InfluenceHelper.SealCost(10, 15));
        Assert.Equal(3, InfluenceHelper.SealCost(12, 17));
    }

    [Fact]
    public void SealCost_CrossingFiveAndTen_CostsThreeSeals()
    {
        Assert.Equal(3, InfluenceHelper.SealCost(4, 10));
        Assert.Equal(3, InfluenceHelper.SealCost(0, 10));
    }

    [Fact]
    public void SealCost_CrossingTenAndFifteen_CostsFiveSeals()
    {
        Assert.Equal(5, InfluenceHelper.SealCost(5, 15));
        Assert.Equal(5, InfluenceHelper.SealCost(8, 16));
    }

    [Fact]
    public void SealCost_CrossingAllThreeThresholds_CostsSixSeals()
    {
        Assert.Equal(6, InfluenceHelper.SealCost(0, 15));
        Assert.Equal(6, InfluenceHelper.SealCost(1, 20));
    }

    [Fact]
    public void SealCost_ZeroGain_ReturnsZero()
    {
        Assert.Equal(0, InfluenceHelper.SealCost(0, 0));
        Assert.Equal(0, InfluenceHelper.SealCost(5, 5));
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_ZeroGain_ReturnsFalse_DoesNotChangeState()
    {
        var player = new Player { Name = "Alice", Influence = 3 };
        var state  = new GameState(new List<Player> { player });
        var events = new List<IDomainEvent>();

        bool pending = InfluenceHelper.Apply(player, 0, state, events);

        Assert.False(pending);
        Assert.Equal(3, player.Influence);
        Assert.Empty(events);
    }

    [Fact]
    public void Apply_NegativeGain_ReturnsFalse_DoesNotChangeState()
    {
        var player = new Player { Name = "Alice", Influence = 3 };
        var state  = new GameState(new List<Player> { player });
        var events = new List<IDomainEvent>();

        bool pending = InfluenceHelper.Apply(player, -1, state, events);

        Assert.False(pending);
        Assert.Equal(3, player.Influence);
    }

    [Fact]
    public void Apply_GainWithNoThreshold_AppliesDirectly()
    {
        var player = new Player { Name = "Alice", Influence = 2 };
        var state  = new GameState(new List<Player> { player });
        var events = new List<IDomainEvent>();

        bool pending = InfluenceHelper.Apply(player, 2, state, events);

        Assert.False(pending);
        Assert.Equal(4, player.Influence);
        Assert.Equal(1, player.InfluenceGainOrder);
        Assert.Empty(events);
    }

    [Fact]
    public void Apply_GainCrossesThreshold_SetsPendingState()
    {
        var player = new Player { Name = "Alice", Influence = 3 };
        var state  = new GameState(new List<Player> { player });
        var events = new List<IDomainEvent>();

        bool pending = InfluenceHelper.Apply(player, 3, state, events);  // 3→6 crosses threshold 5

        Assert.True(pending);
        Assert.Equal(3, player.PendingInfluenceGain);
        Assert.Equal(1, player.PendingInfluenceSealCost);
        Assert.Equal(3, player.Influence);  // not changed yet
        Assert.Single(events);
        Assert.IsType<InfluenceGainPendingEvent>(events[0]);
    }

    [Fact]
    public void Apply_GainCrossesMultipleThresholds_AccumulatesSealCost()
    {
        var player = new Player { Name = "Alice", Influence = 0 };
        var state  = new GameState(new List<Player> { player });
        var events = new List<IDomainEvent>();

        bool pending = InfluenceHelper.Apply(player, 15, state, events);  // crosses 5+10+15

        Assert.True(pending);
        Assert.Equal(6, player.PendingInfluenceSealCost);
    }

    [Fact]
    public void Apply_DirectGain_BumpsInfluenceGainCounter()
    {
        var player1 = new Player { Name = "Alice", Influence = 0 };
        var player2 = new Player { Name = "Bob",   Influence = 0 };
        var state   = new GameState(new List<Player> { player1, player2 });
        var events  = new List<IDomainEvent>();

        InfluenceHelper.Apply(player1, 2, state, events);
        InfluenceHelper.Apply(player2, 2, state, events);

        Assert.Equal(1, player1.InfluenceGainOrder);
        Assert.Equal(2, player2.InfluenceGainOrder);
        Assert.Equal(2, state.InfluenceGainCounter);
    }
}

/// <summary>
/// Unit tests for ChooseInfluencePayHandler — validation and apply paths.
/// </summary>
public class ChooseInfluencePayHandlerTests
{
    private static (Player Alice, GameState State, ChooseInfluencePayHandler Handler)
        MakeState(int pendingGain = 3, int sealCost = 1, int seals = 5, int influence = 3)
    {
        var alice = new Player
        {
            Name                     = "Alice",
            PendingInfluenceGain     = pendingGain,
            PendingInfluenceSealCost = sealCost,
            DaimyoSeals              = seals,
            Influence                = influence,
        };
        var state = new GameState(new List<Player> { alice });
        state.CurrentPhase = Phase.WorkerPlacement;
        return (alice, state, new ChooseInfluencePayHandler());
    }

    // ── Validation — guard failures ───────────────────────────────────────────

    [Fact]
    public void Validate_NoPendingInfluenceGain_Zero_Fails()
    {
        // PendingInfluenceGain=0 → <= 0 → should fail (kills mutation < 0)
        var (alice, state, handler) = MakeState(pendingGain: 0);
        var result = handler.Validate(new ChooseInfluencePayAction(alice.Id, WillPay: true), state);
        Assert.False(result.IsValid);
        Assert.Contains("pending", result.Reason);
    }

    [Fact]
    public void Validate_PendingInfluenceGain_One_Succeeds()
    {
        // PendingInfluenceGain=1 → > 0 → valid (kills mutation >= 0 or > 1)
        var (alice, state, handler) = MakeState(pendingGain: 1, sealCost: 0, seals: 5);
        var result = handler.Validate(new ChooseInfluencePayAction(alice.Id, WillPay: false), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WillPay_SealsExact_Succeeds()
    {
        // seals == sealCost → exactly enough → valid (kills < changed to <=)
        var (alice, state, handler) = MakeState(pendingGain: 3, sealCost: 2, seals: 2);
        var result = handler.Validate(new ChooseInfluencePayAction(alice.Id, WillPay: true), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WillPay_OneShortOnSeals_Fails()
    {
        // seals == sealCost - 1 → one short → fails
        var (alice, state, handler) = MakeState(pendingGain: 3, sealCost: 2, seals: 1);
        var result = handler.Validate(new ChooseInfluencePayAction(alice.Id, WillPay: true), state);
        Assert.False(result.IsValid);
        Assert.Contains("seals", result.Reason);
    }

    [Fact]
    public void Validate_WillNotPay_SealsInsufficient_Succeeds()
    {
        // WillPay=false bypasses seal check even with 0 seals
        var (alice, state, handler) = MakeState(pendingGain: 3, sealCost: 5, seals: 0);
        var result = handler.Validate(new ChooseInfluencePayAction(alice.Id, WillPay: false), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WrongPhase_Fails()
    {
        var (alice, state, handler) = MakeState();
        state.CurrentPhase = Phase.Setup;
        var result = handler.Validate(new ChooseInfluencePayAction(alice.Id, WillPay: false), state);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_UnknownPlayer_Fails()
    {
        var (_, state, handler) = MakeState();
        var result = handler.Validate(new ChooseInfluencePayAction(Guid.NewGuid(), WillPay: false), state);
        Assert.False(result.IsValid);
        Assert.Contains("Unknown", result.Reason);
    }

    // ── Apply — WillPay = true ────────────────────────────────────────────────

    [Fact]
    public void Apply_WillPay_DeductsSealsAndAddsInfluence()
    {
        var (alice, state, handler) = MakeState(pendingGain: 3, sealCost: 2, seals: 5, influence: 3);
        var events = new List<IDomainEvent>();
        handler.Apply(new ChooseInfluencePayAction(alice.Id, WillPay: true), state, events);

        Assert.Equal(3, alice.DaimyoSeals); // 5 - 2
        Assert.Equal(6, alice.Influence);   // 3 + 3
    }

    [Fact]
    public void Apply_WillPay_ClearsPendingState()
    {
        var (alice, state, handler) = MakeState(pendingGain: 3, sealCost: 1, seals: 5);
        var events = new List<IDomainEvent>();
        handler.Apply(new ChooseInfluencePayAction(alice.Id, WillPay: true), state, events);

        Assert.Equal(0, alice.PendingInfluenceGain);
        Assert.Equal(0, alice.PendingInfluenceSealCost);
    }

    [Fact]
    public void Apply_WillPay_BumpsInfluenceGainOrder()
    {
        var (alice, state, handler) = MakeState();
        var events = new List<IDomainEvent>();
        handler.Apply(new ChooseInfluencePayAction(alice.Id, WillPay: true), state, events);

        Assert.Equal(1, alice.InfluenceGainOrder);
        Assert.Equal(1, state.InfluenceGainCounter);
    }

    [Fact]
    public void Apply_WillPay_EmitsResolvedEvent_SealsPaid()
    {
        var (alice, state, handler) = MakeState(pendingGain: 3, sealCost: 2, seals: 5);
        var events = new List<IDomainEvent>();
        handler.Apply(new ChooseInfluencePayAction(alice.Id, WillPay: true), state, events);

        var evt = Assert.Single(events.OfType<InfluenceGainResolvedEvent>());
        Assert.Equal(state.GameId, evt.GameId);
        Assert.Equal(alice.Id,     evt.PlayerId);
        Assert.Equal(3,            evt.InfluenceGain);
        Assert.Equal(2,            evt.SealsPaid);
        Assert.True(evt.Accepted);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
    }

    // ── Apply — WillPay = false ───────────────────────────────────────────────

    [Fact]
    public void Apply_WillNotPay_InfluenceAndSealsUnchanged()
    {
        var (alice, state, handler) = MakeState(pendingGain: 3, sealCost: 2, seals: 5, influence: 3);
        var events = new List<IDomainEvent>();
        handler.Apply(new ChooseInfluencePayAction(alice.Id, WillPay: false), state, events);

        Assert.Equal(5, alice.DaimyoSeals); // unchanged
        Assert.Equal(3, alice.Influence);   // unchanged
    }

    [Fact]
    public void Apply_WillNotPay_ClearsPendingState()
    {
        var (alice, state, handler) = MakeState(pendingGain: 3, sealCost: 1, seals: 5);
        var events = new List<IDomainEvent>();
        handler.Apply(new ChooseInfluencePayAction(alice.Id, WillPay: false), state, events);

        Assert.Equal(0, alice.PendingInfluenceGain);
        Assert.Equal(0, alice.PendingInfluenceSealCost);
    }

    [Fact]
    public void Apply_WillNotPay_EmitsResolvedEvent_ZeroSealsPaid()
    {
        var (alice, state, handler) = MakeState(pendingGain: 3, sealCost: 2, seals: 5);
        var events = new List<IDomainEvent>();
        handler.Apply(new ChooseInfluencePayAction(alice.Id, WillPay: false), state, events);

        var evt = Assert.Single(events.OfType<InfluenceGainResolvedEvent>());
        Assert.Equal(0,     evt.SealsPaid);
        Assert.False(evt.Accepted);
    }
}

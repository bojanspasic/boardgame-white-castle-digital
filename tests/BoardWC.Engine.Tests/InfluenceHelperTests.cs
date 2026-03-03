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

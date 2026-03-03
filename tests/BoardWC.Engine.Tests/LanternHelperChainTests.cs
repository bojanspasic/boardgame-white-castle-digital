using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for LanternHelper — FireChain CardGainType cases, empty-chain early return,
/// and Apply/Trigger entry points.
/// </summary>
public class LanternHelperChainTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static Player MakePlayerWithChain(params (CardGainType Type, int Amount)[] gains)
    {
        var player = new Player { Name = "Alice" };
        player.LanternChain.Add(new LanternChainItem
        {
            SourceCardId   = "test-card",
            SourceCardType = "Test",
            Gains          = gains.Select(g => new LanternChainGain(g.Type, g.Amount))
                                  .ToList().AsReadOnly(),
        });
        return player;
    }

    private static Guid AnyGameId => Guid.NewGuid();

    // ── Apply — lanternAmount <= 0 does NOT fire chain ────────────────────────

    [Fact]
    public void Apply_ZeroLanternAmount_DoesNotFireChain()
    {
        var player = MakePlayerWithChain((CardGainType.Food, 1));
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, 0, AnyGameId, events);

        Assert.Equal(0, player.LanternScore);
        Assert.Empty(events.OfType<LanternChainActivatedEvent>());
    }

    [Fact]
    public void Apply_NegativeLanternAmount_DoesNotFireChain()
    {
        var player = MakePlayerWithChain((CardGainType.Food, 1));
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, -1, AnyGameId, events);

        // Score decremented (though unusual) but chain NOT fired
        Assert.Equal(-1, player.LanternScore);
        Assert.Empty(events.OfType<LanternChainActivatedEvent>());
    }

    // ── Apply — positive amount increments score and fires chain ──────────────

    [Fact]
    public void Apply_PositiveLanternAmount_IncrementsScoreAndFiresChain()
    {
        var player = MakePlayerWithChain((CardGainType.Food, 2));
        var events = new List<IDomainEvent>();
        var gameId = AnyGameId;

        LanternHelper.Apply(player, 1, gameId, events);

        Assert.Equal(1, player.LanternScore);
        Assert.Equal(2, player.Resources.Food);

        var evt = Assert.Single(events.OfType<LanternChainActivatedEvent>());
        Assert.Equal(gameId,   evt.GameId);
        Assert.Equal(player.Id, evt.PlayerId);
        Assert.Equal(2,         evt.Resources.Food);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
    }

    // ── Trigger — fires chain without incrementing score ─────────────────────

    [Fact]
    public void Trigger_WithChain_FiresChainWithoutIncrementingScore()
    {
        var player = MakePlayerWithChain((CardGainType.Coin, 1));
        var events = new List<IDomainEvent>();
        var gameId = AnyGameId;

        LanternHelper.Trigger(player, gameId, events);

        Assert.Equal(0, player.LanternScore); // score unchanged
        Assert.Equal(1, player.Coins);

        Assert.Single(events.OfType<LanternChainActivatedEvent>());
    }

    // ── FireChain — empty chain early return ──────────────────────────────────

    [Fact]
    public void Apply_EmptyChain_DoesNotEmitChainActivatedEvent()
    {
        var player = new Player { Name = "Alice" }; // no chain items
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, 1, AnyGameId, events);

        // Score incremented but chain event NOT emitted
        Assert.Equal(1, player.LanternScore);
        Assert.Empty(events.OfType<LanternChainActivatedEvent>());
    }

    [Fact]
    public void Trigger_EmptyChain_DoesNotEmitChainActivatedEvent()
    {
        var player = new Player { Name = "Alice" };
        var events = new List<IDomainEvent>();

        LanternHelper.Trigger(player, AnyGameId, events);

        Assert.Empty(events.OfType<LanternChainActivatedEvent>());
    }

    // ── FireChain — all CardGainType cases ────────────────────────────────────

    [Fact]
    public void FireChain_FoodGain_GrantsFood()
    {
        var player = MakePlayerWithChain((CardGainType.Food, 3));
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, 1, AnyGameId, events);

        Assert.Equal(3, player.Resources.Food);
        var evt = Assert.Single(events.OfType<LanternChainActivatedEvent>());
        Assert.Equal(3, evt.Resources.Food);
        Assert.Equal(0, evt.Resources.Iron);
        Assert.Equal(0, evt.Resources.ValueItem);
        Assert.Equal(0, evt.Coins);
        Assert.Equal(0, evt.Seals);
        Assert.Equal(0, evt.VpGained);
    }

    [Fact]
    public void FireChain_IronGain_GrantsIron()
    {
        var player = MakePlayerWithChain((CardGainType.Iron, 2));
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, 1, AnyGameId, events);

        Assert.Equal(2, player.Resources.Iron);
        var evt = Assert.Single(events.OfType<LanternChainActivatedEvent>());
        Assert.Equal(2, evt.Resources.Iron);
    }

    [Fact]
    public void FireChain_ValueItemGain_GrantsValueItem()
    {
        var player = MakePlayerWithChain((CardGainType.ValueItem, 2));
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, 1, AnyGameId, events);

        Assert.Equal(2, player.Resources.ValueItem);
        var evt = Assert.Single(events.OfType<LanternChainActivatedEvent>());
        Assert.Equal(2, evt.Resources.ValueItem);
    }

    [Fact]
    public void FireChain_CoinGain_GrantsCoin()
    {
        var player = MakePlayerWithChain((CardGainType.Coin, 4));
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, 1, AnyGameId, events);

        Assert.Equal(4, player.Coins);
        var evt = Assert.Single(events.OfType<LanternChainActivatedEvent>());
        Assert.Equal(4, evt.Coins);
    }

    [Fact]
    public void FireChain_MonarchialSealGain_GrantsSeal()
    {
        var player = MakePlayerWithChain((CardGainType.MonarchialSeal, 1));
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, 1, AnyGameId, events);

        Assert.Equal(1, player.MonarchialSeals);
        var evt = Assert.Single(events.OfType<LanternChainActivatedEvent>());
        Assert.Equal(1, evt.Seals);
    }

    [Fact]
    public void FireChain_MonarchialSealGain_CappedAtFive()
    {
        var player = MakePlayerWithChain((CardGainType.MonarchialSeal, 10));
        player.MonarchialSeals = 3; // 3 + 10 = 13, should cap at 5
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, 1, AnyGameId, events);

        Assert.Equal(5, player.MonarchialSeals);
    }

    [Fact]
    public void FireChain_VictoryPointGain_IncrementsLanternScore()
    {
        var player = MakePlayerWithChain((CardGainType.VictoryPoint, 2));
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, 1, AnyGameId, events);

        // LanternScore = 1 (from Apply) + 2 (from VP chain) = 3
        Assert.Equal(3, player.LanternScore);
        var evt = Assert.Single(events.OfType<LanternChainActivatedEvent>());
        Assert.Equal(2, evt.VpGained);
    }

    // ── Multiple chain items ──────────────────────────────────────────────────

    [Fact]
    public void FireChain_MultipleItems_AccumulatesAllGains()
    {
        var player = new Player { Name = "Alice" };

        player.LanternChain.Add(new LanternChainItem
        {
            SourceCardId   = "card1",
            SourceCardType = "GroundFloor",
            Gains          = new[] { new LanternChainGain(CardGainType.Food, 1) }.AsReadOnly(),
        });
        player.LanternChain.Add(new LanternChainItem
        {
            SourceCardId   = "card2",
            SourceCardType = "MidFloor",
            Gains          = new[] { new LanternChainGain(CardGainType.Iron, 2) }.AsReadOnly(),
        });

        var events = new List<IDomainEvent>();
        var gameId = AnyGameId;

        LanternHelper.Apply(player, 1, gameId, events);

        Assert.Equal(1, player.Resources.Food);
        Assert.Equal(2, player.Resources.Iron);

        var evt = Assert.Single(events.OfType<LanternChainActivatedEvent>());
        Assert.Equal(1, evt.Resources.Food);
        Assert.Equal(2, evt.Resources.Iron);
        Assert.Equal(gameId,    evt.GameId);
        Assert.Equal(player.Id, evt.PlayerId);
    }

    // ── Resource clamping at 7 ────────────────────────────────────────────────

    [Fact]
    public void FireChain_ResourceCappedAtSeven()
    {
        var player = MakePlayerWithChain((CardGainType.Food, 10));
        player.Resources = new ResourceBag(Food: 5); // 5 + 10 = 15, clamped to 7
        var events = new List<IDomainEvent>();

        LanternHelper.Apply(player, 1, AnyGameId, events);

        Assert.Equal(7, player.Resources.Food);
    }
}

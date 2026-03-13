using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for the Well gain type in CardFieldHelper — verifies that a GainCardField
/// containing CardGainType.Well fires the full Well token effect.
/// </summary>
public class CardFieldHelperWellGainTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static RoomCard MakeCard(params CardField[] fields) =>
        new RoomCard("w1", new List<CardField>(fields).AsReadOnly());

    private static (Player Alice, GameState State, ChooseCastleCardFieldHandler Handler)
        MakeState(int seals = 2)
    {
        var alice = new Player
        {
            Name        = "Alice",
            DaimyoSeals = seals,
        };
        alice.Pending.CastleCardFieldFilter = "Any";

        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Well, 1) }.AsReadOnly()));

        var state = new GameState(new List<Player> { alice });
        var placeholder = state.Board.GetCastleRoom(0, 0);
        placeholder.AddToken(new Token(BridgeColor.Red, TokenResource.Food));
        placeholder.SetCard(card);

        return (alice, state, new ChooseCastleCardFieldHandler());
    }

    private static void Apply(Player alice, GameState state, ChooseCastleCardFieldHandler handler)
    {
        var events = new List<IDomainEvent>();
        handler.Apply(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state, events);
    }

    private static List<IDomainEvent> ApplyWithEvents(Player alice, GameState state, ChooseCastleCardFieldHandler handler)
    {
        var events = new List<IDomainEvent>();
        handler.Apply(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state, events);
        return events;
    }

    // ── Seal gain ─────────────────────────────────────────────────────────────

    [Fact]
    public void WellGain_GrantsSeal()
    {
        var (alice, state, handler) = MakeState(seals: 2);

        Apply(alice, state, handler);

        Assert.Equal(3, alice.DaimyoSeals);
    }

    [Fact]
    public void WellGain_SealCappedAtFive()
    {
        var (alice, state, handler) = MakeState(seals: 5);

        Apply(alice, state, handler);

        Assert.Equal(5, alice.DaimyoSeals);
    }

    // ── Token resource gains ──────────────────────────────────────────────────

    [Fact]
    public void WellGain_GrantsFood_WhenFoodTokenInWell()
    {
        var (alice, state, handler) = MakeState();
        state.Board.Well.AddToken(new Token(BridgeColor.Red, TokenResource.Food, IsResourceSideUp: true));

        Apply(alice, state, handler);

        Assert.Equal(1, alice.Resources.Food);
    }

    [Fact]
    public void WellGain_GrantsIron_WhenIronTokenInWell()
    {
        var (alice, state, handler) = MakeState();
        state.Board.Well.AddToken(new Token(BridgeColor.Black, TokenResource.Iron, IsResourceSideUp: true));

        Apply(alice, state, handler);

        Assert.Equal(1, alice.Resources.Iron);
    }

    [Fact]
    public void WellGain_GrantsMotherOfPearls_WhenMotherOfPearlsTokenInWell()
    {
        var (alice, state, handler) = MakeState();
        state.Board.Well.AddToken(new Token(BridgeColor.White, TokenResource.MotherOfPearls, IsResourceSideUp: true));

        Apply(alice, state, handler);

        Assert.Equal(1, alice.Resources.MotherOfPearls);
    }

    [Fact]
    public void WellGain_GrantsCoin_WhenCoinTokenInWell()
    {
        var (alice, state, handler) = MakeState();
        state.Board.Well.AddToken(new Token(BridgeColor.Red, TokenResource.Coin, IsResourceSideUp: true));

        Apply(alice, state, handler);

        Assert.Equal(1, alice.Coins);
    }

    [Fact]
    public void WellGain_GrantsPendingChoice_WhenAnyResourceTokenInWell()
    {
        var (alice, state, handler) = MakeState();
        state.Board.Well.AddToken(new Token(BridgeColor.Black, TokenResource.AnyResource, IsResourceSideUp: true));

        Apply(alice, state, handler);

        Assert.Equal(1, alice.Pending.AnyResourceChoices);
    }

    // ── Empty well ────────────────────────────────────────────────────────────

    [Fact]
    public void WellGain_EmptyWell_OnlyGrantsSeal()
    {
        var (alice, state, handler) = MakeState(seals: 1);
        // Well has no tokens

        Apply(alice, state, handler);

        Assert.Equal(2, alice.DaimyoSeals);
        Assert.Equal(0, alice.Coins);
        Assert.Equal(0, alice.Resources.Food);
        Assert.Equal(0, alice.Pending.AnyResourceChoices);
    }

    // ── Event emission ────────────────────────────────────────────────────────

    [Fact]
    public void WellGain_EmitsWellEffectAppliedEvent()
    {
        var (alice, state, handler) = MakeState();
        state.Board.Well.AddToken(new Token(BridgeColor.Red, TokenResource.Food,   IsResourceSideUp: true));
        state.Board.Well.AddToken(new Token(BridgeColor.Red, TokenResource.Coin,   IsResourceSideUp: true));

        var events = ApplyWithEvents(alice, state, handler);

        var evt = Assert.Single(events.OfType<WellEffectAppliedEvent>());
        Assert.Equal(1,     evt.SealGained);
        Assert.Equal(1,     evt.CoinsGained);
        Assert.Equal(1,     evt.ResourcesGained.Food);
        Assert.Equal(0,     evt.PendingChoices);
        Assert.Equal(alice.Id, evt.PlayerId);
    }

    [Fact]
    public void WellGain_AnyResourceToken_ReflectedInEvent()
    {
        var (alice, state, handler) = MakeState();
        state.Board.Well.AddToken(new Token(BridgeColor.Black, TokenResource.AnyResource, IsResourceSideUp: true));

        var events = ApplyWithEvents(alice, state, handler);

        var evt = Assert.Single(events.OfType<WellEffectAppliedEvent>());
        Assert.Equal(1, evt.PendingChoices);
    }
}

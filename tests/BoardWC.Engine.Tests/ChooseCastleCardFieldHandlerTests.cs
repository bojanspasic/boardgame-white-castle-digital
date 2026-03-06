using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for ChooseCastleCardFieldHandler — filter validation and field activation.
/// </summary>
public class ChooseCastleCardFieldHandlerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static RoomCard MakeCard(params CardField[] fields) =>
        new RoomCard("c1", new List<CardField>(fields).AsReadOnly());

    private static (Player Alice, GameState State, ChooseCastleCardFieldHandler Handler)
        MakeState(string filter, int floor = 0, int room = 0,
                  RoomCard? card = null, BridgeColor? tokenColor = null)
    {
        var alice = new Player
        {
            Name                         = "Alice",
            PendingCastleCardFieldFilter = filter,
            Coins                        = 10,
        };
        var state = new GameState(new List<Player> { alice });

        var placeholder = state.Board.GetCastleRoom(floor, room);
        if (tokenColor is { } color)
            placeholder.AddToken(new Token(color, TokenResource.Food));
        if (card is not null)
            placeholder.SetCard(card);

        return (alice, state, new ChooseCastleCardFieldHandler());
    }

    // ── Validation — guard failures ───────────────────────────────────────────

    [Fact]
    public void Validate_NoPendingFilter_Fails()
    {
        var alice   = new Player { Name = "Alice", PendingCastleCardFieldFilter = null };
        var state   = new GameState(new List<Player> { alice });
        var handler = new ChooseCastleCardFieldHandler();

        var result = handler.Validate(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("pending", result.Reason);
    }

    [Fact]
    public void Validate_Skip_IsAlwaysValid()
    {
        var alice   = new Player { Name = "Alice", PendingCastleCardFieldFilter = "Any" };
        var state   = new GameState(new List<Player> { alice });
        var handler = new ChooseCastleCardFieldHandler();

        var result = handler.Validate(new ChooseCastleCardFieldAction(alice.Id, -1, 0, 0), state);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_FilterRed_RoomHasNoRedToken_Fails()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Coin, 2) }.AsReadOnly()));
        // Add a non-red token so the room is not empty but has no red token
        var (alice, state, handler) = MakeState("Red", card: card, tokenColor: BridgeColor.Black);

        var result = handler.Validate(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("red token", result.Reason);
    }

    [Fact]
    public void Validate_FilterBlack_RoomHasNoBlackToken_Fails()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Coin, 2) }.AsReadOnly()));
        var (alice, state, handler) = MakeState("Black", card: card, tokenColor: BridgeColor.Red);

        var result = handler.Validate(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("black token", result.Reason);
    }

    [Fact]
    public void Validate_FilterWhite_RoomHasNoWhiteToken_Fails()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Coin, 2) }.AsReadOnly()));
        var (alice, state, handler) = MakeState("White", card: card, tokenColor: BridgeColor.Red);

        var result = handler.Validate(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("white token", result.Reason);
    }

    [Fact]
    public void Validate_FilterGainOnly_ActionField_Fails()
    {
        var card = MakeCard(new ActionCardField("Play castle", Array.Empty<CardCostItem>()));
        var (alice, state, handler) = MakeState("GainOnly", card: card, tokenColor: BridgeColor.Red);

        var result = handler.Validate(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("not a gain field", result.Reason);
    }

    [Fact]
    public void Validate_FilterGainOnly_GainField_Succeeds()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Coin, 1) }.AsReadOnly()));
        var (alice, state, handler) = MakeState("GainOnly", card: card, tokenColor: BridgeColor.Red);

        var result = handler.Validate(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ActionField_InsufficientCoins_Fails()
    {
        var card = MakeCard(new ActionCardField("Play castle",
            new[] { new CardCostItem(CardCostType.Coin, 15) }.AsReadOnly()));
        var (alice, state, handler) = MakeState("Any", card: card, tokenColor: BridgeColor.Red);
        // alice starts with 10 coins, cost is 15

        var result = handler.Validate(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("coins", result.Reason);
    }

    // ── Apply — skip ──────────────────────────────────────────────────────────

    [Fact]
    public void Apply_Skip_ClearsPendingFilterAndEmitsEvent()
    {
        var alice   = new Player { Name = "Alice", PendingCastleCardFieldFilter = "Any" };
        var state   = new GameState(new List<Player> { alice });
        var handler = new ChooseCastleCardFieldHandler();
        var events  = new List<IDomainEvent>();

        handler.Apply(new ChooseCastleCardFieldAction(alice.Id, -1, 0, 0), state, events);

        Assert.Null(alice.PendingCastleCardFieldFilter);
        var evt = Assert.Single(events.OfType<CastleCardFieldChosenEvent>());
        Assert.Equal(-1, evt.Floor);
        Assert.Equal(-1, evt.FieldIndex);
    }

    // ── Apply — gain field ────────────────────────────────────────────────────

    [Fact]
    public void Apply_GainField_Coin_GrantsCoins()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Coin, 3) }.AsReadOnly()));
        var (alice, state, handler) = MakeState("Any", card: card, tokenColor: BridgeColor.Red);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state, events);

        Assert.Equal(13, alice.Coins); // 10 + 3
        Assert.Null(alice.PendingCastleCardFieldFilter);

        var evt = Assert.Single(events.OfType<CastleCardFieldChosenEvent>());
        Assert.Equal(3, evt.CoinsGained);
        Assert.Equal(0, evt.Floor);
        Assert.Equal(0, evt.FieldIndex);
    }

    [Fact]
    public void Apply_GainField_DaimyoSeal_GrantsSeal()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.DaimyoSeal, 2) }.AsReadOnly()));
        var (alice, state, handler) = MakeState("Any", card: card, tokenColor: BridgeColor.Red);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state, events);

        Assert.Equal(2, alice.DaimyoSeals);
        var evt = Assert.Single(events.OfType<CastleCardFieldChosenEvent>());
        Assert.Equal(2, evt.SealsGained);
    }

    // ── Apply — action field ──────────────────────────────────────────────────

    [Fact]
    public void Apply_ActionField_PlayCastle_SetsCastlePending()
    {
        var card = MakeCard(new ActionCardField("Play castle", Array.Empty<CardCostItem>()));
        var (alice, state, handler) = MakeState("Any", card: card, tokenColor: BridgeColor.Red);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state, events);

        Assert.Equal(1, alice.CastlePlaceRemaining);
        Assert.Equal(1, alice.CastleAdvanceRemaining);
        Assert.Null(alice.PendingCastleCardFieldFilter);

        var evt = Assert.Single(events.OfType<CastleCardFieldChosenEvent>());
        Assert.Equal("Play castle", evt.ActionTriggered);
    }

    [Fact]
    public void Apply_ActionField_PlayFarm_SetsFarmPending()
    {
        var card = MakeCard(new ActionCardField("Play farm", Array.Empty<CardCostItem>()));
        var (alice, state, handler) = MakeState("Any", card: card, tokenColor: BridgeColor.Red);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state, events);

        Assert.Equal(1, alice.PendingFarmActions);
        var evt = Assert.Single(events.OfType<CastleCardFieldChosenEvent>());
        Assert.Equal("Play farm", evt.ActionTriggered);
    }

    [Fact]
    public void Apply_ActionField_WithCoinCost_DeductsCoins()
    {
        var card = MakeCard(new ActionCardField("Play farm",
            new[] { new CardCostItem(CardCostType.Coin, 3) }.AsReadOnly()));
        var (alice, state, handler) = MakeState("Any", card: card, tokenColor: BridgeColor.Red);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state, events);

        Assert.Equal(7, alice.Coins); // 10 - 3
        Assert.Equal(1, alice.PendingFarmActions);
    }

    [Fact]
    public void Apply_ActionField_PlayPersonalDomainRow_SetsRowChoicePending()
    {
        var card = MakeCard(new ActionCardField("Play personal domain row", Array.Empty<CardCostItem>()));
        var (alice, state, handler) = MakeState("Any", card: card, tokenColor: BridgeColor.Red);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseCastleCardFieldAction(alice.Id, 0, 0, 0), state, events);

        Assert.True(alice.PendingPersonalDomainRowChoice);
    }
}

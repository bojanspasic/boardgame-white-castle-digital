using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for ChooseNewCardFieldHandler — validation and field activation on newly acquired cards.
/// </summary>
public class ChooseNewCardFieldHandlerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static RoomCard MakeCard(params CardField[] fields) =>
        new RoomCard("c1", new List<CardField>(fields).AsReadOnly());

    private static (Player Alice, GameState State, ChooseNewCardFieldHandler Handler)
        MakeState(RoomCard? pendingCard = null)
    {
        var alice = new Player
        {
            Name                   = "Alice",
            PendingNewCardActivation = pendingCard,
            Coins                  = 5,
        };
        var state = new GameState(new List<Player> { alice });
        return (alice, state, new ChooseNewCardFieldHandler());
    }

    // ── Validation — guard failures ───────────────────────────────────────────

    [Fact]
    public void Validate_NoPendingCard_Fails()
    {
        var (alice, state, handler) = MakeState(pendingCard: null);

        var result = handler.Validate(new ChooseNewCardFieldAction(alice.Id, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("pending", result.Reason);
    }

    [Fact]
    public void Validate_InvalidFieldIndex_Fails()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Coin, 1) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);

        var result = handler.Validate(new ChooseNewCardFieldAction(alice.Id, 5), state);

        Assert.False(result.IsValid);
        Assert.Contains("Invalid field", result.Reason);
    }

    [Fact]
    public void Validate_Skip_IsAlwaysValid()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Coin, 1) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);

        var result = handler.Validate(new ChooseNewCardFieldAction(alice.Id, -1), state);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ActionField_InsufficientCoins_Fails()
    {
        var card = MakeCard(new ActionCardField("Play castle",
            new[] { new CardCostItem(CardCostType.Coin, 10) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        // alice has 5 coins, cost is 10

        var result = handler.Validate(new ChooseNewCardFieldAction(alice.Id, 0), state);

        Assert.False(result.IsValid);
        Assert.Contains("coins", result.Reason);
    }

    // ── Apply — skip ──────────────────────────────────────────────────────────

    [Fact]
    public void Apply_Skip_AddsCardToPersonalDomainWithNoEffect()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Coin, 3) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseNewCardFieldAction(alice.Id, -1), state, events);

        Assert.Null(alice.PendingNewCardActivation);
        Assert.Single(alice.PersonalDomainCards);
        Assert.Equal(5, alice.Coins); // no coins gained

        var evt = Assert.Single(events.OfType<NewCardFieldChosenEvent>());
        Assert.Equal(-1, evt.FieldIndex);
        Assert.Equal("c1", evt.CardId);
    }

    // ── Apply — gain field ────────────────────────────────────────────────────

    [Fact]
    public void Apply_GainField_Food_GrantsFood()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Food, 3) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseNewCardFieldAction(alice.Id, 0), state, events);

        Assert.Equal(3, alice.Resources.Food);
        Assert.Single(alice.PersonalDomainCards);
        Assert.Null(alice.PendingNewCardActivation);

        var evt = Assert.Single(events.OfType<NewCardFieldChosenEvent>());
        Assert.Equal(0, evt.FieldIndex);
        Assert.Equal(3, evt.ResourcesGained.Food);
    }

    [Fact]
    public void Apply_GainField_DaimyoSeal_GrantsSealCappedAtFive()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.DaimyoSeal, 10) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseNewCardFieldAction(alice.Id, 0), state, events);

        Assert.Equal(5, alice.DaimyoSeals); // capped at 5
    }

    // ── Apply — action field ──────────────────────────────────────────────────

    [Fact]
    public void Apply_ActionField_PlayTrainingGrounds_SetsTrainingGroundsPending()
    {
        var card = MakeCard(new ActionCardField("Play training grounds", Array.Empty<CardCostItem>()));
        var (alice, state, handler) = MakeState(card);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseNewCardFieldAction(alice.Id, 0), state, events);

        Assert.Equal(1, alice.PendingTrainingGroundsActions);
        Assert.Null(alice.PendingNewCardActivation);
        Assert.Single(alice.PersonalDomainCards);

        var evt = Assert.Single(events.OfType<NewCardFieldChosenEvent>());
        Assert.Equal("Play training grounds", evt.ActionTriggered);
    }

    [Fact]
    public void Apply_ActionField_PlayCastle_SetsCastlePending()
    {
        var card = MakeCard(new ActionCardField("Play castle", Array.Empty<CardCostItem>()));
        var (alice, state, handler) = MakeState(card);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseNewCardFieldAction(alice.Id, 0), state, events);

        Assert.Equal(1, alice.CastlePlaceRemaining);
        Assert.Equal(1, alice.CastleAdvanceRemaining);

        var evt = Assert.Single(events.OfType<NewCardFieldChosenEvent>());
        Assert.Equal("Play castle", evt.ActionTriggered);
    }

    [Fact]
    public void Apply_ActionField_WithSealCost_DeductsSeals()
    {
        var card = MakeCard(new ActionCardField("Play castle",
            new[] { new CardCostItem(CardCostType.DaimyoSeal, 2) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        alice.DaimyoSeals = 3;
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseNewCardFieldAction(alice.Id, 0), state, events);

        Assert.Equal(1, alice.DaimyoSeals); // 3 - 2
    }

    // ── Validation — field index boundaries ──────────────────────────────────

    [Fact]
    public void Validate_FieldIndexMinusTwoNotSkip_Fails()
    {
        // FieldIndex=-2 is not the skip sentinel (-1) and is < 0 → invalid
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Coin, 1) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        var result = handler.Validate(new ChooseNewCardFieldAction(alice.Id, -2), state);
        Assert.False(result.IsValid);
        Assert.Contains("Invalid field", result.Reason);
    }

    [Fact]
    public void Validate_ActionField_InsufficientSeals_Fails()
    {
        var card = MakeCard(new ActionCardField("Play castle",
            new[] { new CardCostItem(CardCostType.DaimyoSeal, 3) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        alice.DaimyoSeals = 0;
        var result = handler.Validate(new ChooseNewCardFieldAction(alice.Id, 0), state);
        Assert.False(result.IsValid);
        Assert.Contains("seals", result.Reason);
    }

    [Fact]
    public void Validate_ActionField_ExactSeals_Succeeds()
    {
        // seals == cost → exactly enough → valid (boundary for < changed to <=)
        var card = MakeCard(new ActionCardField("Play castle",
            new[] { new CardCostItem(CardCostType.DaimyoSeal, 2) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        alice.DaimyoSeals = 2;
        var result = handler.Validate(new ChooseNewCardFieldAction(alice.Id, 0), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ActionField_ExactCoins_Succeeds()
    {
        // coins == cost → exactly enough → valid
        var card = MakeCard(new ActionCardField("Play castle",
            new[] { new CardCostItem(CardCostType.Coin, 5) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        // alice starts with 5 coins (from MakeState), cost is 5
        var result = handler.Validate(new ChooseNewCardFieldAction(alice.Id, 0), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnknownPlayer_Fails()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Coin, 1) }.AsReadOnly()));
        var (_, state, handler) = MakeState(card);
        var result = handler.Validate(new ChooseNewCardFieldAction(Guid.NewGuid(), 0), state);
        Assert.False(result.IsValid);
        Assert.Contains("Unknown", result.Reason);
    }

    // ── Apply — coin cost deduction and resource gains ──────────────────────

    [Fact]
    public void Apply_ActionField_WithCoinCost_DeductsCoins()
    {
        var card = MakeCard(new ActionCardField("Play farm",
            new[] { new CardCostItem(CardCostType.Coin, 3) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseNewCardFieldAction(alice.Id, 0), state, events);

        Assert.Equal(2, alice.Coins); // 5 - 3
        Assert.Equal(1, alice.PendingFarmActions);
    }

    [Fact]
    public void Apply_GainField_CastleGainField_SetsPendingFilter()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.CastleGainField, 1) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseNewCardFieldAction(alice.Id, 0), state, events);

        Assert.Equal("GainOnly", alice.PendingCastleCardFieldFilter);
        Assert.Single(alice.PersonalDomainCards);
        Assert.Null(alice.PendingNewCardActivation);

        var evt = Assert.Single(events.OfType<NewCardFieldChosenEvent>());
        Assert.Equal(0, evt.FieldIndex);
    }

    [Fact]
    public void Validate_GainField_CastleGainField_IsValid()
    {
        // Gain fields have no cost — CastleGainField should be valid even with 0 coins/seals
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.CastleGainField, 1) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        alice.Coins = 0;

        var result = handler.Validate(new ChooseNewCardFieldAction(alice.Id, 0), state);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Apply_GainField_Iron_GrantsIron()
    {
        var card = MakeCard(new GainCardField(
            new[] { new CardGainItem(CardGainType.Iron, 3) }.AsReadOnly()));
        var (alice, state, handler) = MakeState(card);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseNewCardFieldAction(alice.Id, 0), state, events);

        Assert.Equal(3, alice.Resources.Iron);
        var evt = Assert.Single(events.OfType<NewCardFieldChosenEvent>());
        Assert.Equal(3, evt.ResourcesGained.Iron);
    }
}

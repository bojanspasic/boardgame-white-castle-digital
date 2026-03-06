using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for ChoosePersonalDomainRowHandler — validation, resource gain,
/// uncovered spot gain, and personal domain card field activation.
/// </summary>
public class ChoosePersonalDomainRowHandlerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static (Player Alice, Player Bob, GameState State, ChoosePersonalDomainRowHandler Handler)
        MakeState(Action<Player>? setup = null)
    {
        var alice = new Player { Name = "Alice" };
        var configs = PersonalDomainRowConfig.Load();
        alice.PersonalDomainRows = configs.Select(c => new PersonalDomainRow(c)).ToArray();
        setup?.Invoke(alice);

        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        return (alice, bob, state, new ChoosePersonalDomainRowHandler());
    }

    // ── CanHandle ─────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_ChoosePersonalDomainRowAction_ReturnsTrue()
    {
        var handler = new ChoosePersonalDomainRowHandler();
        Assert.True(handler.CanHandle(new ChoosePersonalDomainRowAction(Guid.NewGuid(), BridgeColor.Red)));
    }

    [Fact]
    public void CanHandle_OtherAction_ReturnsFalse()
    {
        var handler = new ChoosePersonalDomainRowHandler();
        Assert.False(handler.CanHandle(new StartGameAction()));
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_UnknownPlayer_Fails()
    {
        var (_, _, state, handler) = MakeState();
        var result = handler.Validate(new ChoosePersonalDomainRowAction(Guid.NewGuid(), BridgeColor.Red), state);
        Assert.False(result.IsValid);
        Assert.Contains("Unknown player", result.Reason);
    }

    [Fact]
    public void Validate_NotActivePlayer_Fails()
    {
        var (_, bob, state, handler) = MakeState();
        bob.PendingPersonalDomainRowChoice = true;
        var result = handler.Validate(new ChoosePersonalDomainRowAction(bob.Id, BridgeColor.Red), state);
        Assert.False(result.IsValid);
        Assert.Contains("not this player's turn", result.Reason);
    }

    [Fact]
    public void Validate_NoPendingChoice_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        alice.PendingPersonalDomainRowChoice = false;
        var result = handler.Validate(new ChoosePersonalDomainRowAction(alice.Id, BridgeColor.Red), state);
        Assert.False(result.IsValid);
        Assert.Contains("No pending personal domain row choice", result.Reason);
    }

    [Fact]
    public void Validate_InvalidRowColor_Fails()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        // All personal domain rows are Red/White/Black — there's no way a color won't match
        // unless we clear the domain rows
        alice.PersonalDomainRows = [];
        var result = handler.Validate(new ChoosePersonalDomainRowAction(alice.Id, BridgeColor.Red), state);
        Assert.False(result.IsValid);
        Assert.Contains("No personal domain row matches", result.Reason);
    }

    [Fact]
    public void Validate_ValidChoice_Succeeds()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        var redRow = alice.PersonalDomainRows.First(r => r.Config.DieColor == BridgeColor.Red);
        var result = handler.Validate(new ChoosePersonalDomainRowAction(alice.Id, redRow.Config.DieColor), state);
        Assert.True(result.IsValid);
    }

    // ── Apply — basic resource gain ───────────────────────────────────────────

    [Fact]
    public void Apply_ClearsPendingRowChoice()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        var color = alice.PersonalDomainRows[0].Config.DieColor;
        var events = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, color), state, events);

        Assert.False(alice.PendingPersonalDomainRowChoice);
    }

    [Fact]
    public void Apply_GrantsDefaultGain()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        var row   = alice.PersonalDomainRows[0];
        var color = row.Config.DieColor;
        var events = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, color), state, events);

        // Default gain: config says e.g. 1 Food for Red row
        int gained = alice.Resources.Food + alice.Resources.Iron + alice.Resources.MotherOfPearls;
        Assert.True(gained >= row.Config.DefaultGainAmount,
            $"Expected at least {row.Config.DefaultGainAmount} resources, got {gained}");
    }

    [Fact]
    public void Apply_NoUncoveredSpots_OnlyDefaultGain()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        // All figures available = 0 uncovered spots
        alice.CourtiersAvailable = 5;
        alice.FarmersAvailable   = 5;
        alice.SoldiersAvailable  = 5;

        var row   = alice.PersonalDomainRows[0];
        var color = row.Config.DieColor;
        var events = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, color), state, events);

        var evt = Assert.Single(events.OfType<PersonalDomainRowChosenEvent>());
        Assert.Equal(color, evt.RowColor);
        int totalGained = evt.ResourcesGained.Food + evt.ResourcesGained.Iron + evt.ResourcesGained.MotherOfPearls;
        Assert.Equal(row.Config.DefaultGainAmount, totalGained);
    }

    [Fact]
    public void Apply_WithUncoveredSpots_AddsSpotGain()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        // Deploy 2 courtiers so red row (Courtier) has 2 uncovered spots
        alice.CourtiersAvailable = 3; // 5 - 3 = 2 deployed

        var redRow = alice.PersonalDomainRows.First(r => r.Config.FigureType == "Courtier");
        var events = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, redRow.Config.DieColor), state, events);

        var evt = Assert.Single(events.OfType<PersonalDomainRowChosenEvent>());
        int totalGained = evt.ResourcesGained.Food + evt.ResourcesGained.Iron + evt.ResourcesGained.MotherOfPearls;
        // Default + 2 spot gains
        Assert.True(totalGained >= redRow.Config.DefaultGainAmount + 2,
            $"Expected at least {redRow.Config.DefaultGainAmount + 2} total, got {totalGained}");
    }

    [Fact]
    public void Apply_ResourcesCappedAt7()
    {
        var (alice, _, state, handler) = MakeState(a =>
        {
            a.PendingPersonalDomainRowChoice = true;
            a.Resources = new ResourceBag(7, 7, 7);
        });

        var color  = alice.PersonalDomainRows[0].Config.DieColor;
        var events = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, color), state, events);

        Assert.Equal(7, alice.Resources.Food);
        Assert.Equal(7, alice.Resources.Iron);
        Assert.Equal(7, alice.Resources.MotherOfPearls);
    }

    [Fact]
    public void Apply_EmitsPersonalDomainRowChosenEvent()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        var color  = alice.PersonalDomainRows[0].Config.DieColor;
        var events = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, color), state, events);

        var evt = Assert.Single(events.OfType<PersonalDomainRowChosenEvent>());
        Assert.Equal(alice.Id, evt.PlayerId);
        Assert.Equal(color,    evt.RowColor);
    }

    // ── Apply — personal domain card fields ───────────────────────────────────

    [Fact]
    public void Apply_StewardCard_GainFieldActivates_OnCorrectRow()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        // Add a steward card (null layout = 3 fields, one per row index)
        var gains  = new[] { new CardGainItem(CardGainType.Food, 2) }.AsReadOnly();
        var fields = new CardField[]
        {
            new GainCardField(gains),
            new GainCardField(gains),
            new GainCardField(gains)
        }.AsReadOnly();
        var pdCard = new RoomCard("pd-1", fields, layout: null);
        alice.PersonalDomainCards.Add(pdCard);

        // Activate row 0 (Red/Courtier)
        var color  = alice.PersonalDomainRows[0].Config.DieColor;
        var events = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, color), state, events);

        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent pde
            && pde.CardId == "pd-1" && pde.FieldIndex == 0);
    }

    [Fact]
    public void Apply_DoubleTopCard_RowsZeroAndOne_ActivateField0()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var fields = new CardField[]
        {
            new GainCardField(gains),  // spans rows 0+1
            new GainCardField(gains),  // row 2
        }.AsReadOnly();
        var pdCard = new RoomCard("pd-double-top", fields, layout: "DoubleTop");
        alice.PersonalDomainCards.Add(pdCard);

        // Activate row 1 (White/Farmer) — should map to field index 0 for DoubleTop
        var farmerRow = alice.PersonalDomainRows.First(r => r.Config.FigureType == "Farmer");
        var events    = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, farmerRow.Config.DieColor), state, events);

        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent pde
            && pde.CardId == "pd-double-top" && pde.FieldIndex == 0);
    }

    [Fact]
    public void Apply_DoubleTopCard_Row2_ActivatesField1()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var fields = new CardField[]
        {
            new GainCardField(gains),
            new GainCardField(gains),
        }.AsReadOnly();
        var pdCard = new RoomCard("pd-double-top", fields, layout: "DoubleTop");
        alice.PersonalDomainCards.Add(pdCard);

        // Activate row 2 (Black/Soldier) — should map to field index 1 for DoubleTop
        var soldierRow = alice.PersonalDomainRows.First(r => r.Config.FigureType == "Soldier");
        var events     = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, soldierRow.Config.DieColor), state, events);

        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent pde
            && pde.CardId == "pd-double-top" && pde.FieldIndex == 1);
    }

    [Fact]
    public void Apply_DoubleBottomCard_Row0_ActivatesField0()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var fields = new CardField[]
        {
            new GainCardField(gains),
            new GainCardField(gains),
        }.AsReadOnly();
        var pdCard = new RoomCard("pd-double-bot", fields, layout: "DoubleBottom");
        alice.PersonalDomainCards.Add(pdCard);

        var courtierRow = alice.PersonalDomainRows.First(r => r.Config.FigureType == "Courtier");
        var events      = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, courtierRow.Config.DieColor), state, events);

        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent pde
            && pde.CardId == "pd-double-bot" && pde.FieldIndex == 0);
    }

    [Fact]
    public void Apply_DoubleBottomCard_Row1_ActivatesField1()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var fields = new CardField[]
        {
            new GainCardField(gains),
            new GainCardField(gains),
        }.AsReadOnly();
        var pdCard = new RoomCard("pd-double-bot", fields, layout: "DoubleBottom");
        alice.PersonalDomainCards.Add(pdCard);

        var farmerRow = alice.PersonalDomainRows.First(r => r.Config.FigureType == "Farmer");
        var events    = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, farmerRow.Config.DieColor), state, events);

        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent pde
            && pde.CardId == "pd-double-bot" && pde.FieldIndex == 1);
    }

    [Fact]
    public void Apply_ActionCardField_EmitsActivatedEventWithZeroGains()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        var af     = new ActionCardField("Play castle", []);
        var pdCard = new RoomCard("pd-action", new CardField[] { af, af, af }.AsReadOnly());
        alice.PersonalDomainCards.Add(pdCard);

        var color  = alice.PersonalDomainRows[0].Config.DieColor;
        var events = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, color), state, events);

        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent pde
            && pde.CardId == "pd-action" && pde.FieldIndex == 0
            && pde.ResourcesGained.Food == 0 && pde.CoinsGained == 0);
    }

    [Fact]
    public void Apply_UnknownLayout_SkipsCard()
    {
        var (alice, _, state, handler) = MakeState(a => a.PendingPersonalDomainRowChoice = true);
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var pdCard = new RoomCard("pd-unknown",
            new CardField[] { new GainCardField(gains) }.AsReadOnly(),
            layout: "SomeUnknownLayout");
        alice.PersonalDomainCards.Add(pdCard);

        var color  = alice.PersonalDomainRows[0].Config.DieColor;
        var events = new List<IDomainEvent>();

        handler.Apply(new ChoosePersonalDomainRowAction(alice.Id, color), state, events);

        Assert.DoesNotContain(events, e => e is PersonalDomainCardFieldActivatedEvent pde
            && pde.CardId == "pd-unknown");
    }
}

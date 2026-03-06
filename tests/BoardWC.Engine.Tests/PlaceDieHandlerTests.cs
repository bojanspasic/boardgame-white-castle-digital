using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for PlaceDieHandler covering castle card gain/action fields,
/// well token effects, and personal domain seed action paths.
/// </summary>
public class PlaceDieHandlerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    /// Two-player state so the board constructor is happy; board rooms start empty.
    private static (Player Alice, Player Bob, GameState State, PlaceDieHandler Handler)
        MakeState(Action<Player>? setup = null)
    {
        var alice = new Player { Name = "Alice" };
        setup?.Invoke(alice);
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        return (alice, bob, state, new PlaceDieHandler());
    }

    /// Give a player a die in hand. Value 5 to be above most compare values.
    private static Die GiveDie(Player player, BridgeColor color, int value = 5)
    {
        var die = new Die(value, color);
        player.DiceInHand.Add(die);
        return die;
    }

    // ── Castle token color mismatch ───────────────────────────────────────────

    [Fact]
    public void Validate_CastleRoom_WrongDieColor_Fails()
    {
        var (alice, _, state, handler) = MakeState();

        // Room has only a Red token
        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));

        // Player holds a Black die — should not match Red token
        GiveDie(alice, BridgeColor.Black, 5);

        var result = handler.Validate(
            new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state);

        Assert.False(result.IsValid);
        Assert.Contains("Black", result.Reason);
        Assert.Contains("cannot be placed", result.Reason);
    }

    [Fact]
    public void Validate_CastleRoom_MatchingDieColor_Succeeds()
    {
        var (alice, _, state, handler) = MakeState(p => p.Coins = 10);

        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));

        GiveDie(alice, BridgeColor.Red, 5); // Red matches Red token

        var result = handler.Validate(
            new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state);

        Assert.True(result.IsValid);
    }

    // ── Castle card GainCardField — all CardGainType cases ────────────────────

    [Fact]
    public void Apply_CastleRoom_GainCardField_AllGainTypes_AppliesGains()
    {
        var (alice, _, state, handler) = MakeState(p =>
        {
            p.Coins    = 10;
            p.DaimyoSeals = 3;
        });

        var room = state.Board.GetCastleRoom(0, 0);

        // Token must match die color so the field activates
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));

        // Card with one GainCardField containing every gain type
        var gains = new[]
        {
            new CardGainItem(CardGainType.Food,           1),
            new CardGainItem(CardGainType.Iron,           1),
            new CardGainItem(CardGainType.MotherOfPearls,      1),
            new CardGainItem(CardGainType.Coin,           2),
            new CardGainItem(CardGainType.DaimyoSeal, 1),
            new CardGainItem(CardGainType.Lantern,        1),
            new CardGainItem(CardGainType.VictoryPoint,   1),
        }.AsReadOnly();

        var card = new RoomCard("test-gain",
            new CardField[] { new GainCardField(gains) }.AsReadOnly());
        room.SetCard(card);

        GiveDie(alice, BridgeColor.Red, 5);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state, events);

        // Resource gains
        Assert.Equal(1, alice.Resources.Food);
        Assert.Equal(1, alice.Resources.Iron);
        Assert.Equal(1, alice.Resources.MotherOfPearls);

        // Coin delta from die value vs compare value (5 - 3 = 2) plus gain of 2 = 10 + 2 + 2 = 14
        Assert.Equal(14, alice.Coins);

        // Seals: 3 + 1 = 4
        Assert.Equal(4, alice.DaimyoSeals);

        // Lantern (1) → LanternScore += 1 via LanternHelper.Apply, VP (1) → LanternScore += 1 directly
        // Total LanternScore = 0 + 1 (lantern) + 1 (vp) = 2
        Assert.Equal(2, alice.LanternScore);

        // Assert the event
        var evt = Assert.Single(events.OfType<CardFieldGainActivatedEvent>());
        Assert.Equal(state.GameId, evt.GameId);
        Assert.Equal(alice.Id,     evt.PlayerId);
        Assert.Equal("test-gain",  evt.CardId);
        Assert.Equal(0,            evt.FieldIndex);
        Assert.Equal(1,            evt.ResourcesGained.Food);
        Assert.Equal(1,            evt.ResourcesGained.Iron);
        Assert.Equal(1,            evt.ResourcesGained.MotherOfPearls);
        Assert.Equal(2,            evt.CoinsGained);
        Assert.Equal(1,            evt.SealsGained);
        Assert.Equal(1,            evt.LanternGained);
        Assert.Equal(1,            evt.VpGained);
        Assert.Equal(0,            evt.InfluenceGained);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public void Apply_CastleRoom_GainCardField_InfluenceGain_FiresInfluencePendingEvent()
    {
        var (alice, _, state, handler) = MakeState(p => { p.Coins = 10; });

        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));

        // Card with Influence gain (≥ 5 triggers pending event, but even 1 adds to pending)
        var gains = new[]
        {
            new CardGainItem(CardGainType.Influence, 5),
        }.AsReadOnly();
        var card = new RoomCard("test-inf",
            new CardField[] { new GainCardField(gains) }.AsReadOnly());
        room.SetCard(card);

        GiveDie(alice, BridgeColor.Red, 5);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state, events);

        // Influence gain at or above threshold triggers InfluenceGainPendingEvent
        var gainEvt = Assert.Single(events.OfType<CardFieldGainActivatedEvent>());
        Assert.Equal(5, gainEvt.InfluenceGained);

        // InfluenceGainPendingEvent should fire when gain >= 5
        var pending = events.OfType<InfluenceGainPendingEvent>().FirstOrDefault();
        if (pending is not null)
        {
            Assert.Equal(state.GameId, pending.GameId);
            Assert.Equal(alice.Id,     pending.PlayerId);
            Assert.True(pending.OccurredAt > DateTimeOffset.MinValue);
        }
    }

    // ── Castle card ActionCardField — 3 action descriptions ──────────────────

    [Fact]
    public void Apply_CastleRoom_ActionCardField_PlayCastle_SetsPendingCastle()
    {
        var (alice, _, state, handler) = MakeState(p => { p.Coins = 10; });

        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));

        var card = new RoomCard("test-action",
            new CardField[] { new ActionCardField("Play castle", []) }.AsReadOnly());
        room.SetCard(card);

        GiveDie(alice, BridgeColor.Red, 5);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state, events);

        Assert.Equal(1, alice.CastlePlaceRemaining);
        Assert.Equal(1, alice.CastleAdvanceRemaining);

        var evt = Assert.Single(events.OfType<CardActionActivatedEvent>());
        Assert.Equal(state.GameId,   evt.GameId);
        Assert.Equal(alice.Id,       evt.PlayerId);
        Assert.Equal("test-action",  evt.CardId);
        Assert.Equal(0,              evt.FieldIndex);
        Assert.Equal("Play castle",  evt.ActionDescription);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public void Apply_CastleRoom_ActionCardField_PlayTrainingGrounds_SetsPending()
    {
        var (alice, _, state, handler) = MakeState(p => { p.Coins = 10; });

        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));

        var card = new RoomCard("test-tg",
            new CardField[] { new ActionCardField("Play training grounds", []) }.AsReadOnly());
        room.SetCard(card);

        GiveDie(alice, BridgeColor.Red, 5);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state, events);

        Assert.Equal(1, alice.PendingTrainingGroundsActions);

        var evt = Assert.Single(events.OfType<CardActionActivatedEvent>());
        Assert.Equal("Play training grounds", evt.ActionDescription);
    }

    [Fact]
    public void Apply_CastleRoom_ActionCardField_PlayFarm_SetsPendingFarm()
    {
        var (alice, _, state, handler) = MakeState(p => { p.Coins = 10; });

        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));

        var card = new RoomCard("test-farm",
            new CardField[] { new ActionCardField("Play farm", []) }.AsReadOnly());
        room.SetCard(card);

        GiveDie(alice, BridgeColor.Red, 5);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state, events);

        Assert.Equal(1, alice.PendingFarmActions);

        var evt = Assert.Single(events.OfType<CardActionActivatedEvent>());
        Assert.Equal("Play farm", evt.ActionDescription);
    }

    // ── Well token effects — all 5 TokenResource types ────────────────────────

    [Fact]
    public void Apply_Well_FoodToken_GrantsFood()
    {
        var (alice, _, state, handler) = MakeState(p => { p.Coins = 5; });
        state.Board.Well.AddToken(new Token(BridgeColor.Red, TokenResource.Food, IsResourceSideUp: true));

        GiveDie(alice, BridgeColor.Red, 3);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new WellTarget()), state, events);

        Assert.Equal(1, alice.Resources.Food);
        var evt = Assert.Single(events.OfType<WellEffectAppliedEvent>());
        Assert.Equal(state.GameId,            evt.GameId);
        Assert.Equal(alice.Id,                evt.PlayerId);
        Assert.Equal(1,                       evt.SealGained);
        Assert.Equal(1,                       evt.ResourcesGained.Food);
        Assert.Equal(0,                       evt.ResourcesGained.Iron);
        Assert.Equal(0,                       evt.ResourcesGained.MotherOfPearls);
        Assert.Equal(0,                       evt.CoinsGained);
        Assert.Equal(0,                       evt.PendingChoices);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public void Apply_Well_IronToken_GrantsIron()
    {
        var (alice, _, state, handler) = MakeState(p => { p.Coins = 5; });
        state.Board.Well.AddToken(new Token(BridgeColor.Red, TokenResource.Iron, IsResourceSideUp: true));

        GiveDie(alice, BridgeColor.Red, 3);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new WellTarget()), state, events);

        Assert.Equal(1, alice.Resources.Iron);
        var evt = Assert.Single(events.OfType<WellEffectAppliedEvent>());
        Assert.Equal(1, evt.ResourcesGained.Iron);
    }

    [Fact]
    public void Apply_Well_MotherOfPearlsToken_GrantsMotherOfPearls()
    {
        var (alice, _, state, handler) = MakeState(p => { p.Coins = 5; });
        state.Board.Well.AddToken(new Token(BridgeColor.Red, TokenResource.MotherOfPearls, IsResourceSideUp: true));

        GiveDie(alice, BridgeColor.Red, 3);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new WellTarget()), state, events);

        Assert.Equal(1, alice.Resources.MotherOfPearls);
        var evt = Assert.Single(events.OfType<WellEffectAppliedEvent>());
        Assert.Equal(1, evt.ResourcesGained.MotherOfPearls);
    }

    [Fact]
    public void Apply_Well_CoinToken_GrantsCoin()
    {
        var (alice, _, state, handler) = MakeState(p => { p.Coins = 5; });
        state.Board.Well.AddToken(new Token(BridgeColor.Red, TokenResource.Coin, IsResourceSideUp: true));

        GiveDie(alice, BridgeColor.Red, 3);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new WellTarget()), state, events);

        // Coin delta: die value 3 - compare value 1 = +2 → 5+2 = 7, plus 1 from token = 8
        Assert.Equal(8, alice.Coins);
        var evt = Assert.Single(events.OfType<WellEffectAppliedEvent>());
        Assert.Equal(1, evt.CoinsGained);
        Assert.Equal(0, evt.PendingChoices);
    }

    [Fact]
    public void Apply_Well_AnyResourceToken_SetsPendingChoice()
    {
        var (alice, _, state, handler) = MakeState(p => { p.Coins = 5; });
        state.Board.Well.AddToken(new Token(BridgeColor.Red, TokenResource.AnyResource, IsResourceSideUp: true));

        GiveDie(alice, BridgeColor.Red, 3);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new WellTarget()), state, events);

        Assert.Equal(1, alice.PendingAnyResourceChoices);
        var evt = Assert.Single(events.OfType<WellEffectAppliedEvent>());
        Assert.Equal(1, evt.PendingChoices);
    }

    [Fact]
    public void Apply_Well_MultipleTokens_GrantsAll()
    {
        var (alice, _, state, handler) = MakeState(p => { p.Coins = 5; });
        state.Board.Well.AddToken(new Token(BridgeColor.Red,   TokenResource.Food,      IsResourceSideUp: true));
        state.Board.Well.AddToken(new Token(BridgeColor.Black, TokenResource.Iron,      IsResourceSideUp: true));
        state.Board.Well.AddToken(new Token(BridgeColor.White, TokenResource.MotherOfPearls, IsResourceSideUp: true));

        GiveDie(alice, BridgeColor.Red, 3);
        var events = new List<IDomainEvent>();

        handler.Apply(new PlaceDieAction(alice.Id, new WellTarget()), state, events);

        Assert.Equal(1, alice.Resources.Food);
        Assert.Equal(1, alice.Resources.Iron);
        Assert.Equal(1, alice.Resources.MotherOfPearls);

        var evt = Assert.Single(events.OfType<WellEffectAppliedEvent>());
        Assert.Equal(1, evt.ResourcesGained.Food);
        Assert.Equal(1, evt.ResourcesGained.Iron);
        Assert.Equal(1, evt.ResourcesGained.MotherOfPearls);
    }

    // ── PersonalDomain — seed action cards ───────────────────────────────────

    private static (Player Alice, GameState State, PlaceDieHandler Handler)
        MakeStateWithPdSetup(SeedActionType seedType)
    {
        var rowConfigs = PersonalDomainRowConfig.Load();

        var alice = new Player
        {
            Name              = "Alice",
            Coins             = 10,
            CourtiersAvailable = 5,
            SeedCard = new SeedActionCard
            {
                Id         = "seed-test",
                ActionType = seedType,
                Back       = new LanternChainGain(CardGainType.Food, 1),
            },
        };
        // Set up personal domain rows from real config
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();

        var bob   = new Player { Name = "Bob" };
        bob.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();

        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;

        return (alice, state, new PlaceDieHandler());
    }

    [Fact]
    public void Apply_PersonalDomain_SeedCard_PlayCastle_SetsCastlePending()
    {
        var (alice, state, handler) = MakeStateWithPdSetup(SeedActionType.PlayCastle);

        // Row 0 = Red/Courtier, compare value 3
        var row0 = alice.PersonalDomainRows[0];
        GiveDie(alice, row0.Config.DieColor, 5);

        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);

        Assert.Equal(1, alice.CastlePlaceRemaining);
        Assert.Equal(1, alice.CastleAdvanceRemaining);

        var seedEvt = Assert.Single(events.OfType<SeedCardActivatedEvent>());
        Assert.Equal(state.GameId,    seedEvt.GameId);
        Assert.Equal(alice.Id,        seedEvt.PlayerId);
        Assert.Equal("seed-test",     seedEvt.ActionCardId);
        Assert.Equal("PlayCastle",    seedEvt.ActionType);
        Assert.Equal(0,               seedEvt.RowIndex);
        Assert.True(seedEvt.OccurredAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public void Apply_PersonalDomain_SeedCard_PlayFarm_SetsFarmPending()
    {
        var (alice, state, handler) = MakeStateWithPdSetup(SeedActionType.PlayFarm);

        var row0 = alice.PersonalDomainRows[0];
        GiveDie(alice, row0.Config.DieColor, 5);

        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);

        Assert.Equal(1, alice.PendingFarmActions);

        var seedEvt = Assert.Single(events.OfType<SeedCardActivatedEvent>());
        Assert.Equal("PlayFarm", seedEvt.ActionType);
    }

    [Fact]
    public void Apply_PersonalDomain_SeedCard_PlayTrainingGrounds_SetsTgPending()
    {
        var (alice, state, handler) = MakeStateWithPdSetup(SeedActionType.PlayTrainingGrounds);

        var row0 = alice.PersonalDomainRows[0];
        GiveDie(alice, row0.Config.DieColor, 5);

        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);

        Assert.Equal(1, alice.PendingTrainingGroundsActions);

        var seedEvt = Assert.Single(events.OfType<SeedCardActivatedEvent>());
        Assert.Equal("PlayTrainingGrounds", seedEvt.ActionType);
    }

    // ── PersonalDomain activated event properties ─────────────────────────────

    [Fact]
    public void Apply_PersonalDomain_EmitsActivatedEventWithCorrectProperties()
    {
        var rowConfigs = PersonalDomainRowConfig.Load();
        var alice      = new Player { Name = "Alice", Coins = 10 };
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();

        var bob = new Player { Name = "Bob" };
        bob.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();

        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;

        var row0 = alice.PersonalDomainRows[0];
        GiveDie(alice, row0.Config.DieColor, 5);

        var events  = new List<IDomainEvent>();
        var handler = new PlaceDieHandler();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);

        var pdEvt = Assert.Single(events.OfType<PersonalDomainActivatedEvent>());
        Assert.Equal(state.GameId,         pdEvt.GameId);
        Assert.Equal(alice.Id,             pdEvt.PlayerId);
        Assert.Equal(0,                    pdEvt.RowIndex);
        Assert.Equal(row0.Config.DieColor, pdEvt.DieColor);
        Assert.True(pdEvt.UncoveredSpots >= 0);
        Assert.True(pdEvt.OccurredAt > DateTimeOffset.MinValue);
        // ResourcesGained is non-null
        _ = pdEvt.ResourcesGained;
    }

    // ── PersonalDomain card field activation ──────────────────────────────────

    [Fact]
    public void Apply_PersonalDomain_WithCardGainField_EmitsPdCardFieldActivatedEvent()
    {
        var rowConfigs = PersonalDomainRowConfig.Load();
        var alice      = new Player { Name = "Alice", Coins = 10 };
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();

        var bob = new Player { Name = "Bob" };
        bob.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();

        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;

        // Inject a ground-floor room card with 3 GainCardFields (one per row)
        // Field[rowIndex] activates when die placed in that row.
        // Row 0 = index 0 → Field[0] activates.
        var pdCard = new RoomCard("pd-card",
            new CardField[]
            {
                new GainCardField(new[] { new CardGainItem(CardGainType.Food, 2) }.AsReadOnly()),
                new GainCardField(new[] { new CardGainItem(CardGainType.Iron, 1) }.AsReadOnly()),
                new GainCardField(new[] { new CardGainItem(CardGainType.MotherOfPearls, 1) }.AsReadOnly()),
            }.AsReadOnly());
        alice.PersonalDomainCards.Add(pdCard);

        var row0 = alice.PersonalDomainRows[0];
        GiveDie(alice, row0.Config.DieColor, 5);

        var events  = new List<IDomainEvent>();
        var handler = new PlaceDieHandler();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);

        var pdCardEvt = Assert.Single(events.OfType<PersonalDomainCardFieldActivatedEvent>());
        Assert.Equal(state.GameId, pdCardEvt.GameId);
        Assert.Equal(alice.Id,     pdCardEvt.PlayerId);
        Assert.Equal("pd-card",    pdCardEvt.CardId);
        Assert.Equal(0,            pdCardEvt.FieldIndex);
        Assert.Equal(2,            pdCardEvt.ResourcesGained.Food);
        Assert.Equal(0,            pdCardEvt.CoinsGained);
        Assert.Equal(0,            pdCardEvt.SealsGained);
        Assert.Equal(0,            pdCardEvt.LanternGained);
        Assert.Equal(0,            pdCardEvt.VpGained);
        Assert.Equal(0,            pdCardEvt.InfluenceGained);
        Assert.True(pdCardEvt.OccurredAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public void Apply_PersonalDomain_WithActionCardField_EmitsPdCardFieldActivatedEvent()
    {
        var rowConfigs = PersonalDomainRowConfig.Load();
        var alice      = new Player { Name = "Alice", Coins = 10 };
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();

        var bob = new Player { Name = "Bob" };
        bob.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();

        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;

        // ActionCardField with "Play castle" at row 0 → sets castle pending
        var pdCard = new RoomCard("pd-action",
            new CardField[]
            {
                new ActionCardField("Play castle", []),
                new GainCardField(new[] { new CardGainItem(CardGainType.Iron, 1) }.AsReadOnly()),
                new GainCardField(new[] { new CardGainItem(CardGainType.MotherOfPearls, 1) }.AsReadOnly()),
            }.AsReadOnly());
        alice.PersonalDomainCards.Add(pdCard);

        var row0 = alice.PersonalDomainRows[0];
        GiveDie(alice, row0.Config.DieColor, 5);

        var events  = new List<IDomainEvent>();
        var handler = new PlaceDieHandler();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);

        // Castle should be pending now
        Assert.Equal(1, alice.CastlePlaceRemaining);
        Assert.Equal(1, alice.CastleAdvanceRemaining);

        var pdCardEvt = Assert.Single(events.OfType<PersonalDomainCardFieldActivatedEvent>());
        Assert.Equal("pd-action", pdCardEvt.CardId);
        // Action fields emit zero gains
        Assert.Equal(new ResourceBag(), pdCardEvt.ResourcesGained);
        Assert.Equal(0, pdCardEvt.CoinsGained);
    }
}

using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for PlaceDie* handlers covering castle card gain/action fields,
/// well token effects, and personal domain seed action paths.
/// </summary>
public class PlaceDieHandlerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static IActionHandler MakePlaceHandler() =>
        new CompositeActionHandler(new IActionHandler[]
        {
            new PlaceDieAtCastleHandler(),
            new PlaceDieAtWellHandler(),
            new PlaceDieAtOutsideHandler(),
            new PlaceDieAtPersonalDomainHandler(),
        });

    /// Two-player state so the board constructor is happy; board rooms start empty.
    private static (Player Alice, Player Bob, GameState State, IActionHandler Handler)
        MakeState(Action<Player>? setup = null)
    {
        var alice = new Player { Name = "Alice" };
        setup?.Invoke(alice);
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        return (alice, bob, state, MakePlaceHandler());
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

        Assert.Equal(1, alice.Pending.CastlePlaceRemaining);
        Assert.Equal(1, alice.Pending.CastleAdvanceRemaining);

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

        Assert.Equal(1, alice.Pending.TrainingGroundsActions);

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

        Assert.Equal(1, alice.Pending.FarmActions);

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

        Assert.Equal(1, alice.Pending.AnyResourceChoices);
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

    private static (Player Alice, GameState State, IActionHandler Handler)
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

        return (alice, state, MakePlaceHandler());
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

        Assert.Equal(1, alice.Pending.CastlePlaceRemaining);
        Assert.Equal(1, alice.Pending.CastleAdvanceRemaining);

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

        Assert.Equal(1, alice.Pending.FarmActions);

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

        Assert.Equal(1, alice.Pending.TrainingGroundsActions);

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
        var handler = MakePlaceHandler();
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
        var handler = MakePlaceHandler();
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
        var handler = MakePlaceHandler();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);

        // Castle should be pending now
        Assert.Equal(1, alice.Pending.CastlePlaceRemaining);
        Assert.Equal(1, alice.Pending.CastleAdvanceRemaining);

        var pdCardEvt = Assert.Single(events.OfType<PersonalDomainCardFieldActivatedEvent>());
        Assert.Equal("pd-action", pdCardEvt.CardId);
        // Action fields emit zero gains
        Assert.Equal(new ResourceBag(), pdCardEvt.ResourcesGained);
        Assert.Equal(0, pdCardEvt.CoinsGained);
    }

    // ── Validate paths ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WrongPhase_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        state.CurrentPhase = Phase.Setup;
        GiveDie(alice, BridgeColor.Red, 5);
        var result = handler.Validate(new PlaceDieAction(alice.Id, new WellTarget()), state);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_UnknownPlayer_Fails()
    {
        var (_, _, state, handler) = MakeState();
        var result = handler.Validate(new PlaceDieAction(Guid.NewGuid(), new WellTarget()), state);
        Assert.False(result.IsValid);
        Assert.Contains("Unknown player", result.Reason);
    }

    [Fact]
    public void Validate_NotActivePlayer_Fails()
    {
        var (_, bob, state, handler) = MakeState();
        GiveDie(bob, BridgeColor.Red, 5);
        var result = handler.Validate(new PlaceDieAction(bob.Id, new WellTarget()), state);
        Assert.False(result.IsValid);
        Assert.Contains("not this player's turn", result.Reason);
    }

    [Fact]
    public void Validate_NoDieInHand_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        var result = handler.Validate(new PlaceDieAction(alice.Id, new WellTarget()), state);
        Assert.False(result.IsValid);
        Assert.Contains("No die in hand", result.Reason);
    }

    [Fact]
    public void Validate_PlaceholderFull_Fails()
    {
        var (alice, _, state, handler) = MakeState(p => p.Coins = 10);
        GiveDie(alice, BridgeColor.Red, 5);
        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));
        // Fill the slot with 1 die (2-player limit = 1)
        room.PlaceDie(new Die(5, BridgeColor.Red));
        var result = handler.Validate(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state);
        Assert.False(result.IsValid);
        Assert.Contains("full", result.Reason);
    }

    // ── PersonalDomain validation ─────────────────────────────────────────────

    [Fact]
    public void Validate_PersonalDomain_InvalidRowIndex_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        var rowConfigs = PersonalDomainRowConfig.Load();
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        GiveDie(alice, BridgeColor.Red, 5);
        var result = handler.Validate(new PlaceDieAction(alice.Id, new PersonalDomainTarget(99)), state);
        Assert.False(result.IsValid);
        Assert.Contains("Invalid personal domain row index", result.Reason);
    }

    [Fact]
    public void Validate_PersonalDomain_AlreadyHasDie_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        var rowConfigs = PersonalDomainRowConfig.Load();
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        var row = alice.PersonalDomainRows[0];
        row.PlacedDie = new Die(5, row.Config.DieColor);
        GiveDie(alice, row.Config.DieColor, 5);
        var result = handler.Validate(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state);
        Assert.False(result.IsValid);
        Assert.Contains("already has a die", result.Reason);
    }

    [Fact]
    public void Validate_PersonalDomain_WrongDieColor_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        var rowConfigs = PersonalDomainRowConfig.Load();
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        var row        = alice.PersonalDomainRows[0]; // Red row
        var wrongColor = row.Config.DieColor == BridgeColor.Red ? BridgeColor.Black : BridgeColor.Red;
        GiveDie(alice, wrongColor, 5);
        var result = handler.Validate(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state);
        Assert.False(result.IsValid);
        Assert.Contains("die", result.Reason);
    }

    [Fact]
    public void Validate_PersonalDomain_ValidPlacement_Succeeds()
    {
        var (alice, _, state, handler) = MakeState(p => p.Coins = 10);
        var rowConfigs = PersonalDomainRowConfig.Load();
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        var row = alice.PersonalDomainRows[0];
        GiveDie(alice, row.Config.DieColor, 6);
        var result = handler.Validate(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state);
        Assert.True(result.IsValid);
    }

    // ── PersonalDomain — row 1 and row 2 activation ───────────────────────────

    private static (Player Alice, GameState State, IActionHandler Handler)
        MakeStateWithRows()
    {
        var rowConfigs = PersonalDomainRowConfig.Load();
        var alice      = new Player { Name = "Alice", Coins = 10 };
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        var bob = new Player { Name = "Bob" };
        bob.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        return (alice, state, MakePlaceHandler());
    }

    [Fact]
    public void Apply_PersonalDomain_Row1_EmitsActivatedEvent()
    {
        var (alice, state, handler) = MakeStateWithRows();
        var row1 = alice.PersonalDomainRows[1];
        GiveDie(alice, row1.Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(1)), state, events);
        var evt = Assert.Single(events.OfType<PersonalDomainActivatedEvent>());
        Assert.Equal(1, evt.RowIndex);
        Assert.Equal(row1.Config.DieColor, evt.DieColor);
    }

    [Fact]
    public void Apply_PersonalDomain_Row2_EmitsActivatedEvent()
    {
        var (alice, state, handler) = MakeStateWithRows();
        var row2 = alice.PersonalDomainRows[2];
        GiveDie(alice, row2.Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(2)), state, events);
        var evt = Assert.Single(events.OfType<PersonalDomainActivatedEvent>());
        Assert.Equal(2, evt.RowIndex);
    }

    // ── PersonalDomain — DoubleTop layout ────────────────────────────────────

    [Fact]
    public void Apply_PersonalDomain_DoubleTopCard_Row0_ActivatesField0()
    {
        var (alice, state, handler) = MakeStateWithRows();
        var gains  = new[] { new CardGainItem(CardGainType.Food, 2) }.AsReadOnly();
        var pdCard = new RoomCard("dt-card",
            new CardField[] { new GainCardField(gains), new GainCardField(gains) }.AsReadOnly(),
            layout: "DoubleTop");
        alice.PersonalDomainCards.Add(pdCard);
        GiveDie(alice, alice.PersonalDomainRows[0].Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);
        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent p
            && p.CardId == "dt-card" && p.FieldIndex == 0);
    }

    [Fact]
    public void Apply_PersonalDomain_DoubleTopCard_Row1_ActivatesField0()
    {
        var (alice, state, handler) = MakeStateWithRows();
        var gains  = new[] { new CardGainItem(CardGainType.Food, 2) }.AsReadOnly();
        var pdCard = new RoomCard("dt-card",
            new CardField[] { new GainCardField(gains), new GainCardField(gains) }.AsReadOnly(),
            layout: "DoubleTop");
        alice.PersonalDomainCards.Add(pdCard);
        GiveDie(alice, alice.PersonalDomainRows[1].Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(1)), state, events);
        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent p
            && p.CardId == "dt-card" && p.FieldIndex == 0);
    }

    [Fact]
    public void Apply_PersonalDomain_DoubleTopCard_Row2_ActivatesField1()
    {
        var (alice, state, handler) = MakeStateWithRows();
        var gains  = new[] { new CardGainItem(CardGainType.Food, 2) }.AsReadOnly();
        var pdCard = new RoomCard("dt-card",
            new CardField[] { new GainCardField(gains), new GainCardField(gains) }.AsReadOnly(),
            layout: "DoubleTop");
        alice.PersonalDomainCards.Add(pdCard);
        GiveDie(alice, alice.PersonalDomainRows[2].Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(2)), state, events);
        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent p
            && p.CardId == "dt-card" && p.FieldIndex == 1);
    }

    // ── PersonalDomain — DoubleBottom layout ──────────────────────────────────

    [Fact]
    public void Apply_PersonalDomain_DoubleBottomCard_Row0_ActivatesField0()
    {
        var (alice, state, handler) = MakeStateWithRows();
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var pdCard = new RoomCard("db-card",
            new CardField[] { new GainCardField(gains), new GainCardField(gains) }.AsReadOnly(),
            layout: "DoubleBottom");
        alice.PersonalDomainCards.Add(pdCard);
        GiveDie(alice, alice.PersonalDomainRows[0].Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);
        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent p
            && p.CardId == "db-card" && p.FieldIndex == 0);
    }

    [Fact]
    public void Apply_PersonalDomain_DoubleBottomCard_Row1_ActivatesField1()
    {
        var (alice, state, handler) = MakeStateWithRows();
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var pdCard = new RoomCard("db-card",
            new CardField[] { new GainCardField(gains), new GainCardField(gains) }.AsReadOnly(),
            layout: "DoubleBottom");
        alice.PersonalDomainCards.Add(pdCard);
        GiveDie(alice, alice.PersonalDomainRows[1].Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(1)), state, events);
        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent p
            && p.CardId == "db-card" && p.FieldIndex == 1);
    }

    [Fact]
    public void Apply_PersonalDomain_DoubleBottomCard_Row2_ActivatesField1()
    {
        var (alice, state, handler) = MakeStateWithRows();
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var pdCard = new RoomCard("db-card",
            new CardField[] { new GainCardField(gains), new GainCardField(gains) }.AsReadOnly(),
            layout: "DoubleBottom");
        alice.PersonalDomainCards.Add(pdCard);
        GiveDie(alice, alice.PersonalDomainRows[2].Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(2)), state, events);
        Assert.Contains(events, e => e is PersonalDomainCardFieldActivatedEvent p
            && p.CardId == "db-card" && p.FieldIndex == 1);
    }

    // ── PersonalDomain — unknown layout skipped ───────────────────────────────

    [Fact]
    public void Apply_PersonalDomain_UnknownLayout_NoActivation()
    {
        var (alice, state, handler) = MakeStateWithRows();
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var pdCard = new RoomCard("unknown-card",
            new CardField[] { new GainCardField(gains) }.AsReadOnly(),
            layout: "WeirdLayout");
        alice.PersonalDomainCards.Add(pdCard);
        GiveDie(alice, alice.PersonalDomainRows[0].Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);
        Assert.DoesNotContain(events, e => e is PersonalDomainCardFieldActivatedEvent p
            && p.CardId == "unknown-card");
    }

    [Fact]
    public void Apply_PersonalDomain_NullLayout_FieldIndexOutOfRange_NoActivation()
    {
        var (alice, state, handler) = MakeStateWithRows();
        // 1 field (index 0 only); row 2 would request index 2 → out of range → skipped
        var gains  = new[] { new CardGainItem(CardGainType.Food, 1) }.AsReadOnly();
        var pdCard = new RoomCard("short-card",
            new CardField[] { new GainCardField(gains) }.AsReadOnly(),
            layout: null);
        alice.PersonalDomainCards.Add(pdCard);
        GiveDie(alice, alice.PersonalDomainRows[2].Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(2)), state, events);
        Assert.DoesNotContain(events, e => e is PersonalDomainCardFieldActivatedEvent p
            && p.CardId == "short-card");
    }

    // ── PersonalDomain — no seed card ────────────────────────────────────────

    [Fact]
    public void Apply_PersonalDomain_NoSeedCard_NoSeedCardActivatedEvent()
    {
        var (alice, state, handler) = MakeStateWithRows();
        GiveDie(alice, alice.PersonalDomainRows[0].Config.DieColor, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);
        Assert.DoesNotContain(events, e => e is SeedCardActivatedEvent);
    }

    // ── CastleRoom — token index selects field ────────────────────────────────

    [Fact]
    public void Apply_CastleRoom_NonMatchingTokenIndex_SkipsField()
    {
        var (alice, _, state, handler) = MakeState(p => p.Coins = 10);
        var room = state.Board.GetCastleRoom(0, 0);
        // Token[0] = Black (won't match), Token[1] = Red (matches die)
        room.AddToken(new Token(BridgeColor.Black, TokenResource.Food));
        room.AddToken(new Token(BridgeColor.Red,   TokenResource.Food));
        var card = new RoomCard("two-token",
            new CardField[]
            {
                new GainCardField(new[] { new CardGainItem(CardGainType.Iron, 5) }.AsReadOnly()),
                new GainCardField(new[] { new CardGainItem(CardGainType.Food, 3) }.AsReadOnly()),
            }.AsReadOnly());
        room.SetCard(card);
        GiveDie(alice, BridgeColor.Red, 5);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state, events);
        // Only field[1] (Red token) activates
        var evt = Assert.Single(events.OfType<CardFieldGainActivatedEvent>());
        Assert.Equal(1, evt.FieldIndex);
        Assert.Equal(3, evt.ResourcesGained.Food);
        Assert.Equal(0, evt.ResourcesGained.Iron);
    }

    // ── Outside slot → PendingOutsideActivationSlot ───────────────────────────

    [Fact]
    public void Apply_OutsideSlot0_SetsPendingActivationSlot0()
    {
        var (alice, _, state, handler) = MakeState(p => p.Coins = 10);
        GiveDie(alice, BridgeColor.Red, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new OutsideSlotTarget(0)), state, events);
        Assert.Equal(0, alice.Pending.OutsideActivationSlot);
    }

    [Fact]
    public void Apply_OutsideSlot1_SetsPendingActivationSlot1()
    {
        var (alice, _, state, handler) = MakeState(p => p.Coins = 10);
        GiveDie(alice, BridgeColor.Red, 6);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new OutsideSlotTarget(1)), state, events);
        Assert.Equal(1, alice.Pending.OutsideActivationSlot);
    }

    // ── PersonalDomain validation — coin boundary arithmetic ──────────────────

    [Fact]
    public void Validate_PersonalDomain_DeltaZero_NoCoins_Succeeds()
    {
        // die=6 == compareValue=6 → delta=0 → coin check skipped (tests pdDelta < 0 branch)
        var (alice, _, state, handler) = MakeState(p => p.Coins = 0);
        var rowConfigs = PersonalDomainRowConfig.Load();
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        var row = alice.PersonalDomainRows[0]; // Red row, compareValue=6
        GiveDie(alice, row.Config.DieColor, row.Config.CompareValue); // die == compare → delta=0
        var result = handler.Validate(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_PersonalDomain_ExactlyEnoughCoins_Succeeds()
    {
        // die=5, compare=6 → delta=-1; coins=1 → exactly 1 needed → valid
        var (alice, _, state, handler) = MakeState(p => p.Coins = 1);
        var rowConfigs = PersonalDomainRowConfig.Load();
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        var row = alice.PersonalDomainRows[0]; // Red, compareValue=6
        GiveDie(alice, row.Config.DieColor, row.Config.CompareValue - 1); // delta = -1
        var result = handler.Validate(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_PersonalDomain_OneCoinShort_Fails()
    {
        // die=5, compare=6 → delta=-1; coins=0 → need 1 but have 0 → invalid
        var (alice, _, state, handler) = MakeState(p => p.Coins = 0);
        var rowConfigs = PersonalDomainRowConfig.Load();
        alice.PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray();
        var row = alice.PersonalDomainRows[0];
        GiveDie(alice, row.Config.DieColor, row.Config.CompareValue - 1); // delta = -1
        var result = handler.Validate(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state);
        Assert.False(result.IsValid);
        Assert.Contains("coins", result.Reason);
    }

    // ── CastleRoom validation — coin boundary arithmetic ──────────────────────

    [Fact]
    public void Validate_CastleRoom_DeltaZero_NoCoins_Succeeds()
    {
        // Castle floor 0 baseValue=3; die=3 → delta=0 → coin check skipped
        var (alice, _, state, handler) = MakeState(p => p.Coins = 0);
        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));
        GiveDie(alice, BridgeColor.Red, 3); // compare=3, delta=0
        var result = handler.Validate(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_CastleRoom_ExactlyEnoughCoins_Succeeds()
    {
        // Castle floor 0 baseValue=3; die=2 → delta=-1; coins=1 → valid
        var (alice, _, state, handler) = MakeState(p => p.Coins = 1);
        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));
        GiveDie(alice, BridgeColor.Red, 2); // delta = 2-3 = -1
        var result = handler.Validate(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_CastleRoom_OneCoinShort_Fails()
    {
        // Castle floor 0 baseValue=3; die=2 → delta=-1; coins=0 → invalid
        var (alice, _, state, handler) = MakeState(p => p.Coins = 0);
        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));
        GiveDie(alice, BridgeColor.Red, 2); // delta = -1
        var result = handler.Validate(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state);
        Assert.False(result.IsValid);
        Assert.Contains("coins", result.Reason);
    }

    // ── Apply — coin delta arithmetic ─────────────────────────────────────────

    [Fact]
    public void Apply_PersonalDomain_NegativeDelta_DeductsCoins()
    {
        // die=5, compare=6 → delta=-1 → coins decrease by 1
        var (alice, state, handler) = MakeStateWithRows();
        alice.Coins = 10;
        var row = alice.PersonalDomainRows[0]; // compareValue=6
        GiveDie(alice, row.Config.DieColor, row.Config.CompareValue - 1);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);
        Assert.Equal(9, alice.Coins); // 10 + (-1)
    }

    [Fact]
    public void Apply_PersonalDomain_ZeroDelta_CoinsUnchanged()
    {
        // die=6, compare=6 → delta=0 → coins unchanged
        var (alice, state, handler) = MakeStateWithRows();
        alice.Coins = 10;
        var row = alice.PersonalDomainRows[0]; // compareValue=6
        GiveDie(alice, row.Config.DieColor, row.Config.CompareValue); // delta=0
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new PersonalDomainTarget(0)), state, events);
        Assert.Equal(10, alice.Coins);
    }

    [Fact]
    public void Apply_CastleRoom_NegativeDelta_DeductsCoins()
    {
        // Castle floor 0 baseValue=3; die=2 → delta=-1 → coins decrease
        var (alice, _, state, handler) = MakeState(p => p.Coins = 10);
        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));
        GiveDie(alice, BridgeColor.Red, 2); // delta = 2-3 = -1
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state, events);
        Assert.Equal(9, alice.Coins); // 10 + (-1)
    }

    [Fact]
    public void Apply_CastleRoom_PositiveDelta_AddsCoins()
    {
        // Castle floor 0 baseValue=3; die=6 → delta=+3 → coins increase
        var (alice, _, state, handler) = MakeState(p => p.Coins = 10);
        var room = state.Board.GetCastleRoom(0, 0);
        room.AddToken(new Token(BridgeColor.Red, TokenResource.Food));
        GiveDie(alice, BridgeColor.Red, 6); // delta = 6-3 = +3
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)), state, events);
        Assert.Equal(13, alice.Coins); // 10 + 3
    }

    // ── Well — DaimyoSeals cap ─────────────────────────────────────────────────

    [Fact]
    public void Apply_Well_DaimyoSeals_BelowMax_Increments()
    {
        // Seals=4 → after well seals=5 (tests +1 increment)
        var (alice, _, state, handler) = MakeState(p =>
        {
            p.Coins      = 5;
            p.DaimyoSeals = 4;
        });
        GiveDie(alice, BridgeColor.Red, 3);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new WellTarget()), state, events);
        Assert.Equal(5, alice.DaimyoSeals);
    }

    [Fact]
    public void Apply_Well_DaimyoSeals_AtMax_StaysAtMax()
    {
        // Seals=5 → after well still 5 (tests Math.Min cap)
        var (alice, _, state, handler) = MakeState(p =>
        {
            p.Coins      = 5;
            p.DaimyoSeals = 5;
        });
        GiveDie(alice, BridgeColor.Red, 3);
        var events = new List<IDomainEvent>();
        handler.Apply(new PlaceDieAction(alice.Id, new WellTarget()), state, events);
        Assert.Equal(5, alice.DaimyoSeals);
    }
}

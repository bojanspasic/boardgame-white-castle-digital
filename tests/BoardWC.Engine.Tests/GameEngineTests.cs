using BoardWC.Engine.Actions;
using BoardWC.Engine.AI;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Engine;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Tests;

public class GameEngineTests
{
    private static IGameEngine CreateTwoPlayerGame() =>
        GameEngineFactory.Create(
        [
            new PlayerSetup("Alice", PlayerColor.White, IsAI: false),
            new PlayerSetup("Bob",   PlayerColor.Black, IsAI: false),
        ], maxRounds: 3);

    private static IGameEngine StartedGame()
    {
        var engine = CreateTwoPlayerGame();
        engine.ProcessAction(new StartGameAction());
        return engine;
    }

    /// <summary>
    /// Takes the High die from the first available bridge and places it at the Well.
    /// Returns false if no bridge has a High die.
    /// </summary>
    private static bool TakeAndPlaceAtWell(IGameEngine engine)
    {
        var state  = engine.GetCurrentState();
        var player = state.Players[state.ActivePlayerIndex];
        var bridge = state.Board.Bridges.FirstOrDefault(b => b.High != null);
        if (bridge is null) return false;

        engine.ProcessAction(new TakeDieFromBridgeAction(player.Id, bridge.Color, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(player.Id, new WellTarget()));
        return true;
    }

    // ── StartGameAction ──────────────────────────────────────────────────────

    [Fact]
    public void StartGame_TransitionsToWorkerPlacementPhase()
    {
        var engine = CreateTwoPlayerGame();
        Assert.Equal(Phase.Setup, engine.GetCurrentState().CurrentPhase);

        engine.ProcessAction(new StartGameAction());

        Assert.Equal(Phase.WorkerPlacement, engine.GetCurrentState().CurrentPhase);
    }

    [Fact]
    public void StartGame_Twice_ReturnsFail()
    {
        var engine = CreateTwoPlayerGame();
        engine.ProcessAction(new StartGameAction());

        var result = engine.ProcessAction(new StartGameAction());

        Assert.IsType<ActionResult.Failure>(result);
    }

    [Fact]
    public void StartGame_RollsDiceOnAllBridges()
    {
        var engine = StartedGame();
        var board  = engine.GetCurrentState().Board;

        // 2-player game: each bridge gets 3 dice
        foreach (var bridge in board.Bridges)
        {
            var total = (bridge.High  != null ? 1 : 0)
                      + bridge.Middle.Count
                      + (bridge.Low != null ? 1 : 0);
            Assert.Equal(3, total);
        }
    }

    // ── TakeDieFromBridge ────────────────────────────────────────────────────

    [Fact]
    public void TakeDieFromHigh_ValidBridge_Succeeds()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        var result = engine.ProcessAction(
            new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));

        Assert.IsType<ActionResult.Success>(result);
    }

    [Fact]
    public void TakeDieFromHigh_PutsDieInHand()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        engine.ProcessAction(
            new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        Assert.Single(aliceAfter.DiceInHand);
    }

    [Fact]
    public void TakeDieFromHigh_ReducesBridgeDiceCount()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];
        var before = engine.GetCurrentState().Board.TotalDiceRemaining;

        engine.ProcessAction(
            new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));

        Assert.Equal(before - 1, engine.GetCurrentState().Board.TotalDiceRemaining);
    }

    [Fact]
    public void TakeDie_TurnDoesNotAdvanceUntilDiePlaced()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        engine.ProcessAction(
            new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));

        // Still Alice's turn — she has a die to place
        var activeAfter = engine.GetCurrentState().Players[engine.GetCurrentState().ActivePlayerIndex];
        Assert.Equal(alice.Id, activeAfter.Id);
    }

    [Fact]
    public void TakeDieFromLow_EmitsLanternEffectEvent()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        var result = engine.ProcessAction(
            new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.Low));

        var success = Assert.IsType<ActionResult.Success>(result);
        Assert.Contains(success.Events, e => e is LanternEffectFiredEvent);
    }

    [Fact]
    public void TakeDieFromHigh_DoesNotEmitLanternEvent()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        var result = engine.ProcessAction(
            new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));

        var success = Assert.IsType<ActionResult.Success>(result);
        Assert.DoesNotContain(success.Events, e => e is LanternEffectFiredEvent);
    }

    [Fact]
    public void TakeDieFromBridge_EmitsDieTakenEvent()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        var result = engine.ProcessAction(
            new TakeDieFromBridgeAction(alice.Id, BridgeColor.Black, DiePosition.High));

        var success = Assert.IsType<ActionResult.Success>(result);
        Assert.Contains(success.Events, e => e is DieTakenFromBridgeEvent dtb
            && dtb.BridgeColor == BridgeColor.Black
            && dtb.Position == DiePosition.High);
    }

    [Fact]
    public void TakeDieFromBridge_WrongTurn_ReturnsFail()
    {
        var engine = StartedGame();
        var bob    = engine.GetCurrentState().Players[1];

        var result = engine.ProcessAction(
            new TakeDieFromBridgeAction(bob.Id, BridgeColor.Red, DiePosition.High));

        Assert.IsType<ActionResult.Failure>(result);
    }

    [Fact]
    public void TakeDieFromHigh_AfterTaking_MiddleSlidesIntoHigh()
    {
        var engine    = StartedGame();
        var state     = engine.GetCurrentState();
        var alice     = state.Players[0];
        var redBridge = state.Board.Bridges.First(b => b.Color == BridgeColor.Red);

        int? expectedNewHigh = redBridge.Middle.Count > 0 ? redBridge.Middle[0].Value : (int?)null;

        engine.ProcessAction(
            new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));

        var newBridge = engine.GetCurrentState().Board.Bridges.First(b => b.Color == BridgeColor.Red);
        Assert.Equal(expectedNewHigh, newBridge.High?.Value);
    }

    // ── PlaceDie ─────────────────────────────────────────────────────────────

    [Fact]
    public void PlaceDie_WithoutTakingFirst_ReturnsFail()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        var result = engine.ProcessAction(new PlaceDieAction(alice.Id, new WellTarget()));

        Assert.IsType<ActionResult.Failure>(result);
    }

    [Fact]
    public void PlaceDieAtWell_Succeeds()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));
        var result = engine.ProcessAction(new PlaceDieAction(alice.Id, new WellTarget()));

        Assert.IsType<ActionResult.Success>(result);
    }

    [Fact]
    public void PlaceDie_TurnAdvancesAfterPlacement()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(alice.Id, new WellTarget()));

        var activeAfter = engine.GetCurrentState().Players[engine.GetCurrentState().ActivePlayerIndex];
        Assert.NotEqual(alice.Id, activeAfter.Id);
    }

    [Fact]
    public void PlaceDie_DieRemovedFromHand()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(alice.Id, new WellTarget()));

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        Assert.Empty(aliceAfter.DiceInHand);
    }

    [Fact]
    public void PlaceDie_EmitsDiePlacedEvent()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));
        var result = engine.ProcessAction(new PlaceDieAction(alice.Id, new WellTarget()));

        var success = Assert.IsType<ActionResult.Success>(result);
        Assert.Contains(success.Events, e => e is DiePlacedEvent);
    }

    // ── Coin mechanic ─────────────────────────────────────────────────────────

    [Fact]
    public void PlaceDieAtWell_DieHigherThanOne_EarnsCoins()
    {
        var engine  = StartedGame();
        var state   = engine.GetCurrentState();
        var alice   = state.Players[0];
        var redHigh = state.Board.Bridges.First(b => b.Color == BridgeColor.Red).High!;

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(alice.Id, new WellTarget()));

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        Assert.Equal(redHigh.Value - 1, aliceAfter.Coins);
    }

    [Fact]
    public void PlaceDieAtWell_AlwaysNonNegativeCoins()
    {
        // Well compare value = 1; die values are 1–6, so delta is always ≥ 0
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(alice.Id, new WellTarget()));

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        Assert.True(aliceAfter.Coins >= 0);
    }

    [Fact]
    public void PlaceDieInCastle_CannotAfford_ReturnsFail()
    {
        // Castle ground value = 3. A die with value 1 costs 2 coins.
        // Alice starts with 0 coins.
        var engine = StartedGame();
        var state  = engine.GetCurrentState();
        var alice  = state.Players[0];

        var bridgeWith1 = state.Board.Bridges.FirstOrDefault(b => b.High?.Value == 1);
        if (bridgeWith1 is null) return;  // skip if no die value 1 available

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, bridgeWith1.Color, DiePosition.High));
        var result = engine.ProcessAction(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)));

        Assert.IsType<ActionResult.Failure>(result);
    }

    [Fact]
    public void PlaceDieInCastle_HigherDie_EarnsCoins()
    {
        var engine = StartedGame();
        var state  = engine.GetCurrentState();
        var alice  = state.Players[0];

        var bridgeWithHighDie = state.Board.Bridges
            .FirstOrDefault(b => b.High?.Value > 3);
        if (bridgeWithHighDie is null) return;  // skip if no die > 3

        int dieValue = bridgeWithHighDie.High!.Value;
        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, bridgeWithHighDie.Color, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)));

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        Assert.Equal(dieValue - 3, aliceAfter.Coins);
    }

    // ── Capacity ──────────────────────────────────────────────────────────────

    [Fact]
    public void CastleRoom_FullIn2PlayerMode_ReturnsFail()
    {
        var engine = StartedGame();
        var state  = engine.GetCurrentState();
        var alice  = state.Players[0];
        var bob    = state.Players[1];

        // Find any High die with value ≥ 3 for Alice
        var aliceBridge = state.Board.Bridges.FirstOrDefault(b => b.High?.Value >= 3);
        if (aliceBridge is null) { TakeAndPlaceAtWell(engine); return; }  // skip round

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, aliceBridge.Color, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)));

        // Bob tries the same room
        var bobState   = engine.GetCurrentState();
        var bobBridge  = bobState.Board.Bridges.FirstOrDefault(b => b.High?.Value >= 3);
        if (bobBridge is null) return;  // skip if Bob can't afford it either

        engine.ProcessAction(new TakeDieFromBridgeAction(bob.Id, bobBridge.Color, DiePosition.High));
        var result = engine.ProcessAction(new PlaceDieAction(bob.Id, new CastleRoomTarget(0, 0)));

        Assert.IsType<ActionResult.Failure>(result);
    }

    [Fact]
    public void WellUnlimited_BothPlayersCanPlaceAtWell()
    {
        var engine = StartedGame();
        var state  = engine.GetCurrentState();
        var alice  = state.Players[0];
        var bob    = state.Players[1];

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(alice.Id, new WellTarget()));

        engine.ProcessAction(new TakeDieFromBridgeAction(bob.Id, BridgeColor.Black, DiePosition.High));
        var result = engine.ProcessAction(new PlaceDieAction(bob.Id, new WellTarget()));

        Assert.IsType<ActionResult.Success>(result);
        Assert.Equal(2, engine.GetCurrentState().Board.Well.Placeholder.PlacedDice.Count);
    }

    // ── Round end via dice count ──────────────────────────────────────────────

    [Fact]
    public void RoundEnds_WhenTotalDiceDropToThreeOrFewer()
    {
        var engine = StartedGame();

        // Take+place 6 dice — leaves 3, triggering round end
        for (int i = 0; i < 6; i++)
            TakeAndPlaceAtWell(engine);

        Assert.Equal(2, engine.GetCurrentState().CurrentRound);
    }

    [Fact]
    public void NewRound_DiceAreRerolled_AndCountRestored()
    {
        var engine = StartedGame();

        for (int i = 0; i < 6; i++)
            TakeAndPlaceAtWell(engine);

        Assert.Equal(9, engine.GetCurrentState().Board.TotalDiceRemaining);
    }

    [Fact]
    public void EndOfRound_PlacementAreasCleared()
    {
        var engine = StartedGame();

        for (int i = 0; i < 6; i++)
            TakeAndPlaceAtWell(engine);

        var board = engine.GetCurrentState().Board;
        Assert.Equal(0, board.Well.Placeholder.PlacedDice.Count);
        Assert.All(board.Castle.Floors.SelectMany(f => f), ph => Assert.Equal(0, ph.PlacedDice.Count));
        Assert.All(board.Outside.Slots, ph => Assert.Equal(0, ph.PlacedDice.Count));
    }

    [Fact]
    public void AllRoundsComplete_GameOver()
    {
        var engine = GameEngineFactory.Create(
        [
            new PlayerSetup("Alice", PlayerColor.White, IsAI: false),
            new PlayerSetup("Bob",   PlayerColor.Black, IsAI: false),
        ], maxRounds: 1);

        engine.ProcessAction(new StartGameAction());

        for (int i = 0; i < 6; i++)
            TakeAndPlaceAtWell(engine);

        Assert.True(engine.IsGameOver);
    }

    [Fact]
    public void GameOver_FinalScoresAvailable()
    {
        var engine = GameEngineFactory.Create(
        [
            new PlayerSetup("Alice", PlayerColor.White, IsAI: false),
            new PlayerSetup("Bob",   PlayerColor.Black, IsAI: false),
        ], maxRounds: 1);

        engine.ProcessAction(new StartGameAction());

        for (int i = 0; i < 6; i++)
            TakeAndPlaceAtWell(engine);

        var scores = engine.GetFinalScores();
        Assert.NotNull(scores);
        Assert.Equal(2, scores.Count);
    }

    // ── Pass ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Pass_AdvancesTurnToOtherPlayer()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        engine.ProcessAction(new PassAction(alice.Id));

        var activeAfter = engine.GetCurrentState().Players[engine.GetCurrentState().ActivePlayerIndex];
        Assert.NotEqual(alice.Id, activeAfter.Id);
    }

    // ── LegalActions ──────────────────────────────────────────────────────────

    [Fact]
    public void GetLegalActions_BeforeStart_ReturnsStartAction()
    {
        var engine = CreateTwoPlayerGame();
        var alice  = engine.GetCurrentState().Players[0];

        var actions = engine.GetLegalActions(alice.Id);

        Assert.Contains(actions, a => a is StartGameAction);
    }

    [Fact]
    public void GetLegalActions_DuringPlay_ContainsBridgeAndPassOptions()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        var actions = engine.GetLegalActions(alice.Id);

        Assert.Contains(actions, a => a is TakeDieFromBridgeAction);
        Assert.Contains(actions, a => a is PassAction);
    }

    [Fact]
    public void GetLegalActions_WhenDieInHand_OnlyShowsPlacementOptions()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));

        var actions = engine.GetLegalActions(alice.Id);

        Assert.All(actions, a => Assert.IsType<PlaceDieAction>(a));
        Assert.DoesNotContain(actions, a => a is TakeDieFromBridgeAction);
        Assert.DoesNotContain(actions, a => a is PassAction);
    }

    [Fact]
    public void GetLegalActions_BothHighAndLowAvailable_HasTwoActionsPerBridge()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        var actions = engine.GetLegalActions(alice.Id);
        var bridgeActions = actions.OfType<TakeDieFromBridgeAction>().ToList();

        Assert.Equal(6, bridgeActions.Count);
    }

    [Fact]
    public void AllLegalActions_AreAcceptedByEngine()
    {
        var engine  = StartedGame();
        var state   = engine.GetCurrentState();
        var alice   = state.Players[0];
        var actions = engine.GetLegalActions(alice.Id);

        var action = actions.FirstOrDefault(a => a is not PassAction) ?? actions[0];
        var result = engine.ProcessAction(action);

        Assert.IsType<ActionResult.Success>(result);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Fact]
    public void StartGame_EmitsGameStartedEvent()
    {
        var engine = CreateTwoPlayerGame();

        var result = engine.ProcessAction(new StartGameAction());

        var success = Assert.IsType<ActionResult.Success>(result);
        Assert.Contains(success.Events, e => e is GameStartedEvent);
    }

    // ── AI strategies ─────────────────────────────────────────────────────────

    [Fact]
    public void RandomAi_SelectsALegalAction()
    {
        var ai     = new RandomAiStrategy(new Random(42));
        var engine = StartedGame();
        var state  = engine.GetCurrentState();
        var alice  = state.Players[0];
        var legal  = engine.GetLegalActions(alice.Id);

        var chosen = ai.SelectAction(state, legal);

        Assert.Contains(chosen, legal);
    }

    [Fact]
    public void GreedyAi_SelectsALegalAction()
    {
        var ai     = new GreedyResourceAiStrategy();
        var engine = StartedGame();
        var state  = engine.GetCurrentState();
        var alice  = state.Players[0];
        var legal  = engine.GetLegalActions(alice.Id);

        var chosen = ai.SelectAction(state, legal);

        Assert.Contains(chosen, legal);
    }

    // ── ResourceBag ───────────────────────────────────────────────────────────

    [Fact]
    public void ResourceBag_Add_WorksCorrectly()
    {
        var a = new ResourceBag(Iron: 2, ValueItem: 1);
        var b = new ResourceBag(Iron: 1, Food: 3);

        var result = a + b;

        Assert.Equal(3, result.Iron);
        Assert.Equal(1, result.ValueItem);
        Assert.Equal(3, result.Food);
    }

    [Fact]
    public void ResourceBag_CanAfford_TrueWhenSufficient()
    {
        var bag  = new ResourceBag(Iron: 2, Food: 1);
        var cost = new ResourceBag(Iron: 1, Food: 1);
        Assert.True(bag.CanAfford(cost));
    }

    [Fact]
    public void ResourceBag_CanAfford_FalseWhenInsufficient()
    {
        var bag  = new ResourceBag(Iron: 1);
        var cost = new ResourceBag(Iron: 2);
        Assert.False(bag.CanAfford(cost));
    }

    // ── Personal domain ───────────────────────────────────────────────────────

    [Fact]
    public void NewPlayer_HasFiveSoldiersAndCourtiersAndFarmers()
    {
        var engine = CreateTwoPlayerGame();
        var alice  = engine.GetCurrentState().Players[0];

        Assert.Equal(5, alice.SoldiersAvailable);
        Assert.Equal(5, alice.CourtiersAvailable);
        Assert.Equal(5, alice.FarmersAvailable);
    }

    [Fact]
    public void NewPlayer_HasZeroMonarchialSeals()
    {
        var engine = CreateTwoPlayerGame();
        var alice  = engine.GetCurrentState().Players[0];

        Assert.Equal(0, alice.MonarchialSeals);
    }

    // ── Token placement ───────────────────────────────────────────────────────

    [Fact]
    public void StartGame_PlacesThreeTokensInEachGroundRoom()
    {
        var engine = StartedGame();
        var castle = engine.GetCurrentState().Board.Castle;

        foreach (var room in castle.Floors[0])
            Assert.Equal(3, room.Tokens.Count);
    }

    [Fact]
    public void StartGame_PlacesTwoTokensInEachMidRoom()
    {
        var engine = StartedGame();
        var castle = engine.GetCurrentState().Board.Castle;

        foreach (var room in castle.Floors[1])
            Assert.Equal(2, room.Tokens.Count);
    }

    [Fact]
    public void StartGame_PlacesTwoTokensInWell_ResourceSideUp()
    {
        var engine = StartedGame();
        var well   = engine.GetCurrentState().Board.Well.Placeholder;

        Assert.Equal(2, well.Tokens.Count);
        Assert.All(well.Tokens, t => Assert.True(t.IsResourceSideUp));
    }

    [Fact]
    public void StartGame_GroundRooms_HaveAtLeastTwoDifferentColors()
    {
        var engine = StartedGame();
        var castle = engine.GetCurrentState().Board.Castle;

        foreach (var room in castle.Floors[0])
        {
            var distinctColors = room.Tokens.Select(t => t.DieColor).Distinct().Count();
            Assert.True(distinctColors >= 2,
                $"Ground room has only {distinctColors} distinct die color(s); expected ≥2.");
        }
    }

    [Fact]
    public void StartGame_MidRooms_HaveTwoDifferentColors()
    {
        var engine = StartedGame();
        var castle = engine.GetCurrentState().Board.Castle;

        foreach (var room in castle.Floors[1])
        {
            var colors = room.Tokens.Select(t => t.DieColor).Distinct().Count();
            Assert.Equal(2, colors);
        }
    }

    [Fact]
    public void StartGame_TotalTokensAcrossBoardIs15()
    {
        var engine = StartedGame();
        var board  = engine.GetCurrentState().Board;

        var castleTokens = board.Castle.Floors
            .SelectMany(f => f)
            .Sum(ph => ph.Tokens.Count);
        var wellTokens = board.Well.Placeholder.Tokens.Count;

        Assert.Equal(15, castleTokens + wellTokens);
    }
}

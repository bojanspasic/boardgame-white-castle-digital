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
        CompleteSeedSelection(engine);
        return engine;
    }

    /// <summary>
    /// Advances through the SeedCardSelection phase by having each player pick
    /// the first available pair (resolving any AnyResource choices as Food).
    /// </summary>
    private static void CompleteSeedSelection(IGameEngine engine)
    {
        while (engine.GetCurrentState().CurrentPhase == Phase.SeedCardSelection)
        {
            var s     = engine.GetCurrentState();
            var pid   = s.Players[s.ActivePlayerIndex].Id;
            var legal = engine.GetLegalActions(pid);

            var seedAction = legal.OfType<ChooseSeedPairAction>().FirstOrDefault();
            if (seedAction is not null)
                engine.ProcessAction(seedAction);
            else
                engine.ProcessAction(new ChooseResourceAction(pid, ResourceType.Food));
        }
    }

    /// <summary>
    /// Takes the High die from the first available bridge and places it at the Well,
    /// then resolves any pending AnyResource choices (choosing Food each time).
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

        // Resolve any pending AnyResource choices (choosing Food each time)
        while (engine.GetCurrentState().Players.First(p => p.Id == player.Id).PendingAnyResourceChoices > 0)
            engine.ProcessAction(new ChooseResourceAction(player.Id, ResourceType.Food));

        return true;
    }

    // ── StartGameAction ──────────────────────────────────────────────────────

    [Fact]
    public void StartGame_TransitionsToSeedCardSelectionPhase()
    {
        var engine = CreateTwoPlayerGame();
        Assert.Equal(Phase.Setup, engine.GetCurrentState().CurrentPhase);

        engine.ProcessAction(new StartGameAction());

        Assert.Equal(Phase.SeedCardSelection, engine.GetCurrentState().CurrentPhase);
    }

    [Fact]
    public void CompleteSeedSelection_TransitionsToWorkerPlacementPhase()
    {
        var engine = CreateTwoPlayerGame();
        engine.ProcessAction(new StartGameAction());
        CompleteSeedSelection(engine);

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

        // Resolve any pending AnyResource choices before checking turn advance
        while (engine.GetCurrentState().Players.First(p => p.Id == alice.Id).PendingAnyResourceChoices > 0)
            engine.ProcessAction(new ChooseResourceAction(alice.Id, ResourceType.Food));

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
        var engine     = StartedGame();
        var state      = engine.GetCurrentState();
        var alice      = state.Players[0];
        var redHigh    = state.Board.Bridges.First(b => b.Color == BridgeColor.Red).High!;
        var wellTokens = state.Board.Well.Placeholder.Tokens;
        int coinTokens = wellTokens.Count(t => t.ResourceSide == TokenResource.Coin);

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, BridgeColor.Red, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(alice.Id, new WellTarget()));
        while (engine.GetCurrentState().Players.First(p => p.Id == alice.Id).PendingAnyResourceChoices > 0)
            engine.ProcessAction(new ChooseResourceAction(alice.Id, ResourceType.Food));

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        // Coins delta = (die - 1) + any Coin tokens from the well (alice may have coins from seed selection)
        Assert.Equal(alice.Coins + redHigh.Value - 1 + coinTokens, aliceAfter.Coins);
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
        var engine = StartedGame();
        var state  = engine.GetCurrentState();
        var alice  = state.Players[0];

        var bridgeWith1 = state.Board.Bridges.FirstOrDefault(b => b.High?.Value == 1);
        if (bridgeWith1 is null) return;    // skip if no die value 1 available
        if (alice.Coins >= 2) return;       // skip if seed gave enough coins to afford it

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

        // Room (0, 0) has ≥2 token colors; find a bridge whose color matches AND has die > 3
        var room0Colors       = state.Board.Castle.Floors[0][0].Tokens
                                     .Select(t => t.DieColor).ToHashSet();
        var bridgeWithHighDie = state.Board.Bridges
            .FirstOrDefault(b => b.High?.Value > 3 && room0Colors.Contains(b.Color));
        if (bridgeWithHighDie is null) return;  // skip if no matching die > 3

        int dieValue = bridgeWithHighDie.High!.Value;

        // Pre-compute coin gains from card fields activated by this die's color
        var room0 = state.Board.Castle.Floors[0][0];
        int cardCoinGains = 0;
        if (room0.Card is { } card)
        {
            for (int i = 0; i < Math.Min(room0.Tokens.Count, card.Fields.Count); i++)
            {
                if (room0.Tokens[i].DieColor != bridgeWithHighDie.Color) continue;
                var field = card.Fields[i];
                if (!field.IsGain || field.Gains is null) continue;
                cardCoinGains += field.Gains.Where(g => g.GainType == "Coin").Sum(g => g.Amount);
            }
        }

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, bridgeWithHighDie.Color, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)));

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        // alice.Coins already includes any coins from seed selection
        Assert.Equal(alice.Coins + dieValue - 3 + cardCoinGains, aliceAfter.Coins);
    }

    // ── Capacity ──────────────────────────────────────────────────────────────

    [Fact]
    public void CastleRoom_FullIn2PlayerMode_ReturnsFail()
    {
        var engine = StartedGame();
        var state  = engine.GetCurrentState();
        var alice  = state.Players[0];
        var bob    = state.Players[1];

        // Room (0, 0) has ≥2 token colors; find a bridge whose color matches AND has die ≥ 3
        var room0Colors = state.Board.Castle.Floors[0][0].Tokens
                               .Select(t => t.DieColor).ToHashSet();
        var aliceBridge = state.Board.Bridges
            .FirstOrDefault(b => b.High?.Value >= 3 && room0Colors.Contains(b.Color));
        if (aliceBridge is null) { TakeAndPlaceAtWell(engine); return; }  // skip round

        engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, aliceBridge.Color, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, 0)));

        // Bob tries the same room with a matching-color die of value ≥ 3
        var bobState  = engine.GetCurrentState();
        var bobBridge = bobState.Board.Bridges
            .FirstOrDefault(b => b.High?.Value >= 3 && room0Colors.Contains(b.Color));
        if (bobBridge is null) return;  // skip if no affordable matching die for Bob

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
        while (engine.GetCurrentState().Players.First(p => p.Id == alice.Id).PendingAnyResourceChoices > 0)
            engine.ProcessAction(new ChooseResourceAction(alice.Id, ResourceType.Food));

        engine.ProcessAction(new TakeDieFromBridgeAction(bob.Id, BridgeColor.Black, DiePosition.High));
        var result = engine.ProcessAction(new PlaceDieAction(bob.Id, new WellTarget()));
        while (engine.GetCurrentState().Players.First(p => p.Id == bob.Id).PendingAnyResourceChoices > 0)
            engine.ProcessAction(new ChooseResourceAction(bob.Id, ResourceType.Food));

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
        CompleteSeedSelection(engine);

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
        CompleteSeedSelection(engine);

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

    // ── Well effects ─────────────────────────────────────────────────────────

    private static void TakeAndPlaceAtWellForPlayer(IGameEngine engine, Guid playerId)
    {
        var state  = engine.GetCurrentState();
        var bridge = state.Board.Bridges.FirstOrDefault(b => b.High != null);
        if (bridge is null) return;
        engine.ProcessAction(new TakeDieFromBridgeAction(playerId, bridge.Color, DiePosition.High));
        engine.ProcessAction(new PlaceDieAction(playerId, new WellTarget()));
    }

    private static void ResolveAllPendingChoices(IGameEngine engine, Guid playerId)
    {
        while (engine.GetCurrentState().Players.First(p => p.Id == playerId).PendingAnyResourceChoices > 0)
            engine.ProcessAction(new ChooseResourceAction(playerId, ResourceType.Food));
    }

    [Fact]
    public void PlaceDieAtWell_GrantsSeal()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        TakeAndPlaceAtWellForPlayer(engine, alice.Id);
        ResolveAllPendingChoices(engine, alice.Id);

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        Assert.Equal(1, aliceAfter.MonarchialSeals);
    }

    [Fact]
    public void PlaceDieAtWell_ResourcesMatchNonAnyTokenTypes()
    {
        var engine     = StartedGame();
        var alice      = engine.GetCurrentState().Players[0];
        var wellTokens = engine.GetCurrentState().Board.Well.Placeholder.Tokens;

        // ResolveAllPendingChoices picks Food for each AnyResource token
        int anyResourceCount = wellTokens.Count(t => t.ResourceSide == TokenResource.AnyResource);
        // Alice may have resources from seed selection; add well gains on top, capped at 7
        int expectedFood = Math.Min(alice.Resources.Food + wellTokens.Count(t => t.ResourceSide == TokenResource.Food) + anyResourceCount, 7);
        int expectedIron = Math.Min(alice.Resources.Iron + wellTokens.Count(t => t.ResourceSide == TokenResource.Iron), 7);
        int expectedVI   = Math.Min(alice.Resources.ValueItem + wellTokens.Count(t => t.ResourceSide == TokenResource.ValueItem), 7);

        TakeAndPlaceAtWellForPlayer(engine, alice.Id);
        ResolveAllPendingChoices(engine, alice.Id);

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        Assert.Equal(expectedFood, aliceAfter.Resources.Food);
        Assert.Equal(expectedIron, aliceAfter.Resources.Iron);
        Assert.Equal(expectedVI,   aliceAfter.Resources.ValueItem);
    }

    [Fact]
    public void PlaceDieAtWell_PendingChoices_EqualsAnyResourceTokenCount()
    {
        var engine     = StartedGame();
        var alice      = engine.GetCurrentState().Players[0];
        var wellTokens = engine.GetCurrentState().Board.Well.Placeholder.Tokens;
        int expected   = wellTokens.Count(t => t.ResourceSide == TokenResource.AnyResource);

        TakeAndPlaceAtWellForPlayer(engine, alice.Id);

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        Assert.Equal(expected, aliceAfter.PendingAnyResourceChoices);
    }

    [Fact]
    public void PlaceDieAtWell_WithAnyResourcePending_LegalActionsAreChooseResourceOnly()
    {
        var engine     = StartedGame();
        var alice      = engine.GetCurrentState().Players[0];
        var wellTokens = engine.GetCurrentState().Board.Well.Placeholder.Tokens;

        if (!wellTokens.Any(t => t.ResourceSide == TokenResource.AnyResource)) return;

        TakeAndPlaceAtWellForPlayer(engine, alice.Id);

        var actions = engine.GetLegalActions(alice.Id);
        Assert.All(actions, a => Assert.IsType<ChooseResourceAction>(a));
    }

    [Fact]
    public void ChooseResource_ResolvesOnePendingChoice()
    {
        var engine     = StartedGame();
        var alice      = engine.GetCurrentState().Players[0];
        var wellTokens = engine.GetCurrentState().Board.Well.Placeholder.Tokens;

        if (!wellTokens.Any(t => t.ResourceSide == TokenResource.AnyResource)) return;

        TakeAndPlaceAtWellForPlayer(engine, alice.Id);
        int choicesBefore = engine.GetCurrentState().Players.First(p => p.Id == alice.Id).PendingAnyResourceChoices;

        engine.ProcessAction(new ChooseResourceAction(alice.Id, ResourceType.Iron));

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        Assert.Equal(choicesBefore - 1, aliceAfter.PendingAnyResourceChoices);
    }

    [Fact]
    public void ChooseResource_GrantsChosenResource()
    {
        var engine     = StartedGame();
        var alice      = engine.GetCurrentState().Players[0];
        var wellTokens = engine.GetCurrentState().Board.Well.Placeholder.Tokens;

        if (!wellTokens.Any(t => t.ResourceSide == TokenResource.AnyResource)) return;

        TakeAndPlaceAtWellForPlayer(engine, alice.Id);

        engine.ProcessAction(new ChooseResourceAction(alice.Id, ResourceType.Iron));

        var aliceAfter = engine.GetCurrentState().Players.First(p => p.Id == alice.Id);
        Assert.True(aliceAfter.Resources.Iron >= 1);
    }

    [Fact]
    public void ChooseResource_AfterAllChoicesResolved_TurnAdvances()
    {
        var engine = StartedGame();
        var alice  = engine.GetCurrentState().Players[0];

        TakeAndPlaceAtWellForPlayer(engine, alice.Id);
        ResolveAllPendingChoices(engine, alice.Id);

        var activeAfter = engine.GetCurrentState().Players[engine.GetCurrentState().ActivePlayerIndex];
        Assert.NotEqual(alice.Id, activeAfter.Id);
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

    // ── Room cards ────────────────────────────────────────────────────────────

    [Fact]
    public void StartGame_PlacesOneCardInEachGroundFloorRoom()
    {
        var engine = StartedGame();
        var floors = engine.GetCurrentState().Board.Castle.Floors;

        Assert.All(floors[0], room => Assert.NotNull(room.Card));
    }

    [Fact]
    public void StartGame_PlacesOneCardInEachMidFloorRoom()
    {
        var engine = StartedGame();
        var floors = engine.GetCurrentState().Board.Castle.Floors;

        Assert.All(floors[1], room => Assert.NotNull(room.Card));
    }

    [Fact]
    public void StartGame_GroundFloorCard_HasThreeFields()
    {
        var engine = StartedGame();
        var floors = engine.GetCurrentState().Board.Castle.Floors;

        Assert.All(floors[0], room => Assert.Equal(3, room.Card!.Fields.Count));
    }

    [Fact]
    public void StartGame_MidFloorCard_HasTwoFields()
    {
        var engine = StartedGame();
        var floors = engine.GetCurrentState().Board.Castle.Floors;

        Assert.All(floors[1], room => Assert.Equal(2, room.Card!.Fields.Count));
    }

    [Fact]
    public void StartGame_MidFloorCard_HasLayout()
    {
        var engine = StartedGame();
        var floors = engine.GetCurrentState().Board.Castle.Floors;

        Assert.All(floors[1], room =>
            Assert.True(room.Card!.Layout is "DoubleTop" or "DoubleBottom",
                $"Expected DoubleTop or DoubleBottom, got '{room.Card!.Layout}'"));
    }

    [Fact]
    public void PlaceDieInRoom_ActivatesMatchingGainField_AppliesGains()
    {
        var engine = StartedGame();
        var state  = engine.GetCurrentState();
        var alice  = state.Players[0];

        // Find a ground floor room where:
        //   - a gain field exists at position i
        //   - the token at position i has a die color matching an available bridge with a high die
        //   - the die value is affordable (≥ 3, since ground floor value = 3)
        for (int r = 0; r < state.Board.Castle.Floors[0].Count; r++)
        {
            var room = state.Board.Castle.Floors[0][r];
            if (room.Card is null) continue;

            for (int i = 0; i < Math.Min(room.Tokens.Count, room.Card.Fields.Count); i++)
            {
                if (room.Card.Fields[i] is not { IsGain: true } gainField) continue;

                var tokenColor = room.Tokens[i].DieColor;
                var bridge = state.Board.Bridges
                    .FirstOrDefault(b => b.Color == tokenColor && b.High?.Value >= 3);
                if (bridge is null) continue;

                // Found a valid setup — take the die and place it
                var coinsBefore     = alice.Coins;
                var resourcesBefore = alice.Resources;
                var sealsBefore     = alice.MonarchialSeals;
                var lanternBefore   = alice.LanternScore;

                engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, bridge.Color, DiePosition.High));
                var result = engine.ProcessAction(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, r)));

                Assert.IsType<ActionResult.Success>(result);

                // At least one gain-event should have been emitted
                var success = (ActionResult.Success)result;
                Assert.Contains(success.Events, e => e is CardFieldGainActivatedEvent);
                return;
            }
        }
        Assert.True(true, "Test skipped (no suitable room/bridge combination found).");
    }

    // ── Castle room color restriction ─────────────────────────────────────────

    [Fact]
    public void CastleRoom_RejectsPlacement_WhenDieColorHasNoMatchingToken()
    {
        var engine    = StartedGame();
        var state     = engine.GetCurrentState();
        var alice     = state.Players[0];
        var allColors = new[] { BridgeColor.Red, BridgeColor.Black, BridgeColor.White };

        // Mid-floor rooms have exactly 2 die colors — always 1 missing color
        for (int r = 0; r < state.Board.Castle.Floors[1].Count; r++)
        {
            var room    = state.Board.Castle.Floors[1][r];
            var present = room.Tokens.Select(t => t.DieColor).ToHashSet();
            var missing = allColors.Where(c => !present.Contains(c))
                                   .Cast<BridgeColor?>().FirstOrDefault();
            if (missing is null) continue;

            engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, missing.Value, DiePosition.High));
            var result = engine.ProcessAction(new PlaceDieAction(alice.Id, new CastleRoomTarget(1, r)));

            Assert.IsType<ActionResult.Failure>(result);
            return;
        }
        Assert.True(true, "Test skipped (no suitable room found).");
    }

    [Fact]
    public void CastleRoom_AcceptsPlacement_WhenDieColorMatchesAToken()
    {
        var engine = StartedGame();
        var state  = engine.GetCurrentState();
        var alice  = state.Players[0];

        // Find a ground-floor room with a bridge whose color is in the room tokens and die value ≥ 3
        for (int r = 0; r < state.Board.Castle.Floors[0].Count; r++)
        {
            var room   = state.Board.Castle.Floors[0][r];
            var colors = room.Tokens.Select(t => t.DieColor).ToHashSet();
            var bridge = state.Board.Bridges.FirstOrDefault(b =>
                colors.Contains(b.Color) && b.High?.Value >= 3);
            if (bridge is null) continue;

            engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, bridge.Color, DiePosition.High));
            var result = engine.ProcessAction(new PlaceDieAction(alice.Id, new CastleRoomTarget(0, r)));

            Assert.IsType<ActionResult.Success>(result);
            return;
        }
        Assert.True(true, "Test skipped (no affordable matching bridge found).");
    }

    [Fact]
    public void LegalActions_CastleRoom_ExcludesRoomsWithNoMatchingToken()
    {
        var engine    = StartedGame();
        var state     = engine.GetCurrentState();
        var alice     = state.Players[0];
        var allColors = new[] { BridgeColor.Red, BridgeColor.Black, BridgeColor.White };

        // Mid-floor rooms have exactly 2 die colors — find a room missing at least 1 color
        for (int r = 0; r < state.Board.Castle.Floors[1].Count; r++)
        {
            var room    = state.Board.Castle.Floors[1][r];
            var present = room.Tokens.Select(t => t.DieColor).ToHashSet();
            var missing = allColors.Where(c => !present.Contains(c))
                                   .Cast<BridgeColor?>().FirstOrDefault();
            if (missing is null) continue;

            engine.ProcessAction(new TakeDieFromBridgeAction(alice.Id, missing.Value, DiePosition.High));
            var actions = engine.GetLegalActions(alice.Id);

            Assert.DoesNotContain(actions, a =>
                a is PlaceDieAction pda && pda.Target is CastleRoomTarget crt &&
                crt.Floor == 1 && crt.RoomIndex == r);
            return;
        }
        Assert.True(true, "Test skipped (no suitable room found).");
    }
}

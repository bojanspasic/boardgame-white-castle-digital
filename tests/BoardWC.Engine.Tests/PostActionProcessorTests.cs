using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for PostActionProcessor — early-return guards and FirstPlayerByInfluence logic.
/// </summary>
public class PostActionProcessorTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static (Player Alice, Player Bob, GameState State) MakeState(
        Action<Player>? setupAlice = null, Action<Player>? setupBob = null)
    {
        var alice = new Player { Name = "Alice" };
        setupAlice?.Invoke(alice);
        var bob = new Player { Name = "Bob" };
        setupBob?.Invoke(bob);
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        // Make sure there are many dice so round does NOT end from low dice count
        state.Board.RollAllDice(2, new Random(1));
        // Initialize board subsystems so round-end code can run without exceptions
        state.Board.SetupFarmingLands(new Random(1));
        state.Board.SetupTrainingGrounds(new Random(1));
        return (alice, bob, state);
    }

    // ── Hold-turn guards ──────────────────────────────────────────────────────

    [Fact]
    public void Run_DieInHand_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.DiceInHand.Add(new Die(4, BridgeColor.Red));
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_PendingOutsideActivation_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.Pending.OutsideActivationSlot = 0;
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_PendingInfluenceGain_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.Pending.InfluenceGain = 1;
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_PendingAnyResourceChoices_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.Pending.AnyResourceChoices = 1;
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_PendingNewCardActivation_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.Pending.NewCardActivation = new RoomCard("card-1", []);
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_PendingTrainingGroundsActions_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.Pending.TrainingGroundsActions = 1;
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_PendingFarmActions_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.Pending.FarmActions = 1;
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_PendingCastleCardFieldFilter_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.Pending.CastleCardFieldFilter = "Red";
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_PendingPersonalDomainRowChoice_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.Pending.PersonalDomainRowChoice = true;
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_CastlePlaceRemaining_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.Pending.CastlePlaceRemaining = 1;
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_CastleAdvanceRemaining_DoesNotAdvanceTurn()
    {
        var (alice, _, state) = MakeState();
        alice.Pending.CastleAdvanceRemaining = 1;
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    // ── Normal advance ────────────────────────────────────────────────────────

    [Fact]
    public void Run_NoGuardsTriggered_AdvancesToNextPlayer()
    {
        var (_, _, state) = MakeState();
        // Ensure plenty of dice remain so round doesn't end
        state.Board.RollAllDice(2, new Random(1));

        PostActionProcessor.Run(state, []);

        Assert.Equal(1, state.ActivePlayerIndex);
    }

    // ── Non-WorkerPlacement phases ────────────────────────────────────────────

    [Fact]
    public void Run_SetupPhase_DoesNothing()
    {
        var (_, _, state) = MakeState();
        state.CurrentPhase = Phase.Setup;

        PostActionProcessor.Run(state, []);

        Assert.Equal(Phase.Setup, state.CurrentPhase);
    }

    [Fact]
    public void Run_GameOverPhase_DoesNothing()
    {
        var (_, _, state) = MakeState();
        state.CurrentPhase = Phase.GameOver;

        PostActionProcessor.Run(state, []);

        Assert.Equal(Phase.GameOver, state.CurrentPhase);
    }

    // ── FirstPlayerByInfluence (tested via round-end) ─────────────────────────

    [Fact]
    public void Run_RoundEnd_HigherInfluenceGoesFirst()
    {
        var (alice, bob, state) = MakeState(
            a => a.Influence = 5,
            b => { b.Influence = 3; b.InfluenceGainOrder = 10; }
        );
        // Drain all but 3 dice so round ends
        DrainDiceLeaving(state, 3);

        var events = new List<IDomainEvent>();
        PostActionProcessor.Run(state, events);

        // Alice (index 0) has higher influence — should start next round
        Assert.Equal(0, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_RoundEnd_TiedInfluence_MostRecentGainGoesFirst()
    {
        // Bob has the same influence but gained it more recently
        var (alice, bob, state) = MakeState(
            a => { a.Influence = 5; a.InfluenceGainOrder = 1; },
            b => { b.Influence = 5; b.InfluenceGainOrder = 5; }
        );
        DrainDiceLeaving(state, 3);

        var events = new List<IDomainEvent>();
        PostActionProcessor.Run(state, events);

        // Bob (index 1) gained influence more recently — goes first next round
        Assert.Equal(1, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_RoundEnd_NoInfluenceGained_PlayerZeroGoesFirst()
    {
        var (_, _, state) = MakeState();
        state.ActivePlayerIndex = 1;
        DrainDiceLeaving(state, 3);

        var events = new List<IDomainEvent>();
        PostActionProcessor.Run(state, events);

        // No influence — player 0 goes first by default
        Assert.Equal(0, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_RoundEnd_EmitsRoundEndedEvent()
    {
        var (_, _, state) = MakeState();
        DrainDiceLeaving(state, 3);

        var events = new List<IDomainEvent>();
        PostActionProcessor.Run(state, events);

        Assert.Contains(events, e => e is RoundEndedEvent);
    }

    [Fact]
    public void Run_LastRound_TransitionsToGameOver()
    {
        var (alice, bob, state) = MakeState();
        state.CurrentRound = state.MaxRounds; // already at final round
        DrainDiceLeaving(state, 3);

        var events = new List<IDomainEvent>();
        PostActionProcessor.Run(state, events);

        Assert.Equal(Phase.GameOver, state.CurrentPhase);
        Assert.Contains(events, e => e is GameOverEvent);
    }

    // ── SeedCardSelection phase ───────────────────────────────────────────────

    [Fact]
    public void Run_SeedCardSelection_PendingAnyResource_HoldsTurn()
    {
        var (alice, _, state) = MakeState();
        state.CurrentPhase = Phase.SeedCardSelection;
        alice.Pending.AnyResourceChoices = 1;
        int indexBefore = state.ActivePlayerIndex;

        PostActionProcessor.Run(state, []);

        Assert.Equal(indexBefore, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_SeedCardSelection_AllSeedsPicked_TransitionsToWorkerPlacement()
    {
        var (alice, bob, state) = MakeState();
        state.CurrentPhase = Phase.SeedCardSelection;
        // Give both players a seed card so All(p => p.SeedCard is not null) is true
        alice.SeedCard = new SeedActionCard { Id = "s1", ActionType = SeedActionType.PlayFarm };
        bob.SeedCard   = new SeedActionCard { Id = "s2", ActionType = SeedActionType.PlayCastle };

        PostActionProcessor.Run(state, []);

        Assert.Equal(Phase.WorkerPlacement, state.CurrentPhase);
        Assert.Equal(0, state.ActivePlayerIndex);
    }

    [Fact]
    public void Run_SeedCardSelection_NotAllSeedsPicked_AdvancesToNextPlayer()
    {
        var (alice, _, state) = MakeState();
        state.CurrentPhase = Phase.SeedCardSelection;
        alice.SeedCard = new SeedActionCard { Id = "s1", ActionType = SeedActionType.PlayFarm };
        // Bob has no seed card yet

        PostActionProcessor.Run(state, []);

        Assert.Equal(1, state.ActivePlayerIndex);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// Drain dice from bridges until exactly `target` dice remain.
    private static void DrainDiceLeaving(GameState state, int target)
    {
        // Keep removing until at or below target
        foreach (var bridge in state.Board.Bridges)
        {
            while (state.Board.TotalDiceRemaining > target && bridge.CanTakeFromHigh)
                bridge.TakeFromHigh();
            while (state.Board.TotalDiceRemaining > target && bridge.CanTakeFromLow)
                bridge.TakeFromLow();
        }
    }
}

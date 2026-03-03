using BoardWC.Engine.AI;
using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for AiStrategyRegistry, RandomAiStrategy, and GreedyResourceAiStrategy.
/// </summary>
public class AiStrategyTests
{
    // ── AiStrategyRegistry ───────────────────────────────────────────────────

    [Fact]
    public void Registry_Resolve_Random_ReturnsRandomAiStrategy()
    {
        var strategy = AiStrategyRegistry.Resolve("random");
        Assert.IsType<RandomAiStrategy>(strategy);
        Assert.Equal("random", strategy.StrategyId);
    }

    [Fact]
    public void Registry_Resolve_GreedyResource_ReturnsGreedyAiStrategy()
    {
        var strategy = AiStrategyRegistry.Resolve("greedy-resource");
        Assert.IsType<GreedyResourceAiStrategy>(strategy);
        Assert.Equal("greedy-resource", strategy.StrategyId);
    }

    [Fact]
    public void Registry_Resolve_Unknown_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => AiStrategyRegistry.Resolve("unknown-xyz"));
        Assert.Contains("Unknown AI strategy", ex.Message);
        Assert.Contains("unknown-xyz", ex.Message);
    }

    [Fact]
    public void Registry_AvailableStrategies_ContainsBothKeys()
    {
        var available = AiStrategyRegistry.AvailableStrategies;
        Assert.Contains("random",          available);
        Assert.Contains("greedy-resource", available);
    }

    // ── RandomAiStrategy ─────────────────────────────────────────────────────

    [Fact]
    public void RandomAiStrategy_NullRng_UsesRandomShared_NoThrow()
    {
        // Should not throw; null rng defaults to Random.Shared
        var strategy = new RandomAiStrategy(null);
        Assert.Equal("random", strategy.StrategyId);
    }

    [Fact]
    public void RandomAiStrategy_EmptyActions_Throws()
    {
        var strategy = new RandomAiStrategy(new Random(42));
        var snapshot = MakeMinimalSnapshot();
        Assert.Throws<InvalidOperationException>(() => strategy.SelectAction(snapshot, []));
    }

    [Fact]
    public void RandomAiStrategy_SingleAction_ReturnsThatAction()
    {
        var strategy = new RandomAiStrategy(new Random(42));
        var snapshot = MakeMinimalSnapshot();
        IGameAction action = new PassAction(Guid.NewGuid());

        var result = strategy.SelectAction(snapshot, [action]);

        Assert.Same(action, result);
    }

    [Fact]
    public void RandomAiStrategy_MultipleActions_ReturnsOneOfThem()
    {
        var strategy = new RandomAiStrategy(new Random(42));
        var snapshot = MakeMinimalSnapshot();

        IGameAction a1 = new PassAction(Guid.NewGuid());
        IGameAction a2 = new PassAction(Guid.NewGuid());

        var result = strategy.SelectAction(snapshot, [a1, a2]);

        Assert.True(result == a1 || result == a2);
    }

    // ── GreedyResourceAiStrategy ─────────────────────────────────────────────

    [Fact]
    public void GreedyAiStrategy_EmptyActions_Throws()
    {
        var strategy = new GreedyResourceAiStrategy();
        var snapshot = MakeMinimalSnapshot();
        Assert.Throws<InvalidOperationException>(() => strategy.SelectAction(snapshot, []));
    }

    [Fact]
    public void GreedyAiStrategy_PassAction_ScoresNegativeOne()
    {
        var strategy = new GreedyResourceAiStrategy();
        var snapshot = MakeMinimalSnapshot();

        IGameAction pass  = new PassAction(Guid.NewGuid());
        IGameAction other = new TakeDieFromBridgeAction(Guid.NewGuid(), BridgeColor.Red, DiePosition.High);

        // Other (default score 0) beats pass (score -1)
        var result = strategy.SelectAction(snapshot, [pass, other]);

        Assert.Same(other, result);
    }

    [Fact]
    public void GreedyAiStrategy_TakeDieFromBridge_HighDie_ScoresByValue()
    {
        // Build a snapshot with a Red bridge whose High die = 6
        var snapshot = MakeSnapshotWithBridge(BridgeColor.Red, highValue: 6, lowValue: 1);

        var strategy = new GreedyResourceAiStrategy();
        IGameAction high = new TakeDieFromBridgeAction(Guid.NewGuid(), BridgeColor.Red, DiePosition.High);
        IGameAction low  = new TakeDieFromBridgeAction(Guid.NewGuid(), BridgeColor.Red, DiePosition.Low);

        // High (score 6) > Low (score 1)
        var result = strategy.SelectAction(snapshot, [high, low]);

        Assert.Same(high, result);
    }

    [Fact]
    public void GreedyAiStrategy_TakeDieFromBridge_LowDie_ScoresByValue()
    {
        var snapshot = MakeSnapshotWithBridge(BridgeColor.Red, highValue: 6, lowValue: 3);
        var strategy = new GreedyResourceAiStrategy();

        IGameAction low  = new TakeDieFromBridgeAction(Guid.NewGuid(), BridgeColor.Red, DiePosition.Low);
        IGameAction pass = new PassAction(Guid.NewGuid());

        // Low score 3 > pass score -1
        var result = strategy.SelectAction(snapshot, [low, pass]);

        Assert.Same(low, result);
    }

    [Fact]
    public void GreedyAiStrategy_BridgeNotInSnapshot_ScoresZero()
    {
        // Snapshot has no bridges at all
        var snapshot = MakeMinimalSnapshot();
        var strategy = new GreedyResourceAiStrategy();

        IGameAction take = new TakeDieFromBridgeAction(Guid.NewGuid(), BridgeColor.Red, DiePosition.High);
        IGameAction pass = new PassAction(Guid.NewGuid());

        // take scores 0 (bridge null), pass scores -1 → take wins
        var result = strategy.SelectAction(snapshot, [take, pass]);

        Assert.Same(take, result);
    }

    [Fact]
    public void GreedyAiStrategy_UnknownActionType_ScoresZero()
    {
        var snapshot = MakeMinimalSnapshot();
        var strategy = new GreedyResourceAiStrategy();

        // A PlaceDieAction is an "other" action → score 0
        IGameAction place = new PlaceDieAction(Guid.NewGuid(), new WellTarget());
        IGameAction pass  = new PassAction(Guid.NewGuid());

        // place (score 0) beats pass (score -1)
        var result = strategy.SelectAction(snapshot, [place, pass]);

        Assert.Same(place, result);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static GameStateSnapshot MakeMinimalSnapshot()
    {
        return new GameStateSnapshot(
            GameId:            Guid.NewGuid(),
            CurrentPhase:      Phase.WorkerPlacement,
            CurrentRound:      1,
            MaxRounds:         3,
            ActivePlayerIndex: 0,
            Players:           Array.Empty<PlayerSnapshot>().AsReadOnly(),
            Board:             MakeEmptyBoardSnapshot(),
            SeedPairs:         Array.Empty<SeedPairSnapshot>().AsReadOnly());
    }

    private static GameStateSnapshot MakeSnapshotWithBridge(
        BridgeColor color, int highValue, int lowValue)
    {
        var bridge = new BridgeSnapshot(
            color,
            High:   new DieSnapshot(highValue, color),
            Middle: Array.Empty<DieSnapshot>().AsReadOnly(),
            Low:    new DieSnapshot(lowValue,  color));

        var board = new BoardSnapshot(
            Bridges:             new[] { bridge }.AsReadOnly(),
            Castle:              new CastleSnapshot(Array.Empty<IReadOnlyList<DicePlaceholderSnapshot>>(), new TopFloorRoomSnapshot("", Array.Empty<TopFloorSlotSnapshot>().AsReadOnly())),
            Well:                new WellSnapshot(new DicePlaceholderSnapshot(1, true, Array.Empty<DieSnapshot>().AsReadOnly(), Array.Empty<TokenSnapshot>().AsReadOnly(), null)),
            Outside:             new OutsideSnapshot(Array.Empty<DicePlaceholderSnapshot>()),
            GroundFloorDeckRemaining: 0,
            MidFloorDeckRemaining:    0,
            TrainingGrounds:     new TrainingGroundsSnapshot(Array.Empty<TgAreaSnapshot>().AsReadOnly()),
            FarmingLands:        new FarmingLandsSnapshot(Array.Empty<FarmFieldSnapshot>().AsReadOnly()));

        return new GameStateSnapshot(
            GameId:            Guid.NewGuid(),
            CurrentPhase:      Phase.WorkerPlacement,
            CurrentRound:      1,
            MaxRounds:         3,
            ActivePlayerIndex: 0,
            Players:           Array.Empty<PlayerSnapshot>().AsReadOnly(),
            Board:             board,
            SeedPairs:         Array.Empty<SeedPairSnapshot>().AsReadOnly());
    }

    private static BoardSnapshot MakeEmptyBoardSnapshot() =>
        new(
            Bridges:             Array.Empty<BridgeSnapshot>().AsReadOnly(),
            Castle:              new CastleSnapshot(Array.Empty<IReadOnlyList<DicePlaceholderSnapshot>>(), new TopFloorRoomSnapshot("", Array.Empty<TopFloorSlotSnapshot>().AsReadOnly())),
            Well:                new WellSnapshot(new DicePlaceholderSnapshot(1, true, Array.Empty<DieSnapshot>().AsReadOnly(), Array.Empty<TokenSnapshot>().AsReadOnly(), null)),
            Outside:             new OutsideSnapshot(Array.Empty<DicePlaceholderSnapshot>()),
            GroundFloorDeckRemaining: 0,
            MidFloorDeckRemaining:    0,
            TrainingGrounds:     new TrainingGroundsSnapshot(Array.Empty<TgAreaSnapshot>().AsReadOnly()),
            FarmingLands:        new FarmingLandsSnapshot(Array.Empty<FarmFieldSnapshot>().AsReadOnly()));
}

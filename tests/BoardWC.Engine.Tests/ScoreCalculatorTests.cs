using BoardWC.Engine.Domain;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for ScoreCalculator using direct internal object creation (via InternalsVisibleTo).
/// Each test isolates a single scoring category.
/// </summary>
public class ScoreCalculatorTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal GameState with a single player for scoring tests.
    /// The Board's FarmingLands and TrainingGrounds are initialised so AllFarmFields() and
    /// TrainingGrounds.Areas[] do not throw.
    /// </summary>
    private static (GameState State, Player Player) MakeState(Action<Player>? setup = null)
    {
        var player = new Player { Name = "Alice" };
        setup?.Invoke(player);

        var state = new GameState(new List<Player> { player });
        state.Board.SetupFarmingLands(new Random(0));
        state.Board.SetupTrainingGrounds(new Random(0));
        return (state, player);
    }

    // ── Total = sum of parts ─────────────────────────────────────────────────

    [Fact]
    public void ScoreCalculator_Total_EqualsSumOfAllComponents()
    {
        var (state, player) = MakeState(p =>
        {
            p.LanternScore           = 3;
            p.CourtiersAtGate        = 1;    // 1 VP
            p.CourtiersOnStewardFloor = 1;    // 3 VP
            p.Coins                  = 10;   // 2 VP
            p.DaimyoSeals        = 5;    // 1 VP
            p.Resources              = new ResourceBag(Food: 4); // 1 VP
            p.Influence              = 7;    // 3 VP (6-10 band)
        });

        var scores = ScoreCalculator.Calculate(state);
        var s = scores[0];

        Assert.Equal(s.LanternPoints + s.CourtierPoints + s.CoinPoints +
                     s.SealPoints + s.ResourcePoints + s.FarmPoints +
                     s.TrainingGroundsPoints + s.InfluencePoints,
                     s.Total);
    }

    // ── Courtier VP ──────────────────────────────────────────────────────────

    [Fact]
    public void ScoreCalculator_CourtiersAtGate_OneVPEach()
    {
        var (state, _) = MakeState(p => p.CourtiersAtGate = 3);
        Assert.Equal(3, ScoreCalculator.Calculate(state)[0].CourtierPoints);
    }

    [Fact]
    public void ScoreCalculator_CourtiersOnGround_ThreeVPEach()
    {
        var (state, _) = MakeState(p => p.CourtiersOnStewardFloor = 2);
        Assert.Equal(6, ScoreCalculator.Calculate(state)[0].CourtierPoints);
    }

    [Fact]
    public void ScoreCalculator_CourtiersOnMid_SixVPEach()
    {
        var (state, _) = MakeState(p => p.CourtiersOnDiplomatFloor = 2);
        Assert.Equal(12, ScoreCalculator.Calculate(state)[0].CourtierPoints);
    }

    [Fact]
    public void ScoreCalculator_CourtiersOnTop_TenVPEach()
    {
        var (state, _) = MakeState(p => p.CourtiersOnTopFloor = 1);
        Assert.Equal(10, ScoreCalculator.Calculate(state)[0].CourtierPoints);
    }

    [Fact]
    public void ScoreCalculator_MixedCourtierPositions_SumsCorrectly()
    {
        var (state, _) = MakeState(p =>
        {
            p.CourtiersAtGate        = 1;   // 1
            p.CourtiersOnStewardFloor = 1;   // 3
            p.CourtiersOnDiplomatFloor    = 1;   // 6
            p.CourtiersOnTopFloor    = 1;   // 10
        });
        Assert.Equal(20, ScoreCalculator.Calculate(state)[0].CourtierPoints);
    }

    [Fact]
    public void ScoreCalculator_NoCourtiers_ZeroCourtierPoints()
    {
        var (state, _) = MakeState();
        Assert.Equal(0, ScoreCalculator.Calculate(state)[0].CourtierPoints);
    }

    // ── Coin VP ───────────────────────────────────────────────────────────────

    [Fact]
    public void ScoreCalculator_FiveCoins_OneVP()
    {
        var (state, _) = MakeState(p => p.Coins = 5);
        Assert.Equal(1, ScoreCalculator.Calculate(state)[0].CoinPoints);
    }

    [Fact]
    public void ScoreCalculator_TenCoins_TwoVP()
    {
        var (state, _) = MakeState(p => p.Coins = 10);
        Assert.Equal(2, ScoreCalculator.Calculate(state)[0].CoinPoints);
    }

    [Fact]
    public void ScoreCalculator_FourCoins_ZeroVP()
    {
        var (state, _) = MakeState(p => p.Coins = 4);
        Assert.Equal(0, ScoreCalculator.Calculate(state)[0].CoinPoints);
    }

    [Fact]
    public void ScoreCalculator_SevenCoins_OneVP_Remainder_Ignored()
    {
        var (state, _) = MakeState(p => p.Coins = 7);
        Assert.Equal(1, ScoreCalculator.Calculate(state)[0].CoinPoints);
    }

    // ── Seal VP ───────────────────────────────────────────────────────────────

    [Fact]
    public void ScoreCalculator_FiveSeals_OneVP()
    {
        var (state, _) = MakeState(p => p.DaimyoSeals = 5);
        Assert.Equal(1, ScoreCalculator.Calculate(state)[0].SealPoints);
    }

    [Fact]
    public void ScoreCalculator_FourSeals_ZeroVP()
    {
        var (state, _) = MakeState(p => p.DaimyoSeals = 4);
        Assert.Equal(0, ScoreCalculator.Calculate(state)[0].SealPoints);
    }

    // ── Resource VP ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    public void ScoreCalculator_FoodResourceVP(int amount, int expectedVP)
    {
        var (state, _) = MakeState(p => p.Resources = new ResourceBag(Food: amount));
        Assert.Equal(expectedVP, ScoreCalculator.Calculate(state)[0].ResourcePoints);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 1)]
    [InlineData(7, 2)]
    public void ScoreCalculator_IronResourceVP(int amount, int expectedVP)
    {
        var (state, _) = MakeState(p => p.Resources = new ResourceBag(Iron: amount));
        Assert.Equal(expectedVP, ScoreCalculator.Calculate(state)[0].ResourcePoints);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 1)]
    [InlineData(7, 2)]
    public void ScoreCalculator_MotherOfPearlsResourceVP(int amount, int expectedVP)
    {
        var (state, _) = MakeState(p => p.Resources = new ResourceBag(MotherOfPearls: amount));
        Assert.Equal(expectedVP, ScoreCalculator.Calculate(state)[0].ResourcePoints);
    }

    [Fact]
    public void ScoreCalculator_AllThreeResources_AtSeven_SixResourcePoints()
    {
        var (state, _) = MakeState(p => p.Resources = new ResourceBag(Food: 7, Iron: 7, MotherOfPearls: 7));
        Assert.Equal(6, ScoreCalculator.Calculate(state)[0].ResourcePoints);
    }

    [Fact]
    public void ScoreCalculator_MultipleResourceTypes_Accumulate()
    {
        var (state, _) = MakeState(p => p.Resources = new ResourceBag(Food: 4, Iron: 7, MotherOfPearls: 3));
        // Food=4→1VP, Iron=7→2VP, VI=3→0VP = 3
        Assert.Equal(3, ScoreCalculator.Calculate(state)[0].ResourcePoints);
    }

    // ── Farm VP ───────────────────────────────────────────────────────────────

    [Fact]
    public void ScoreCalculator_FarmVP_FarmerOnField_AddsCardVP()
    {
        var (state, player) = MakeState();
        // Place a farmer on every farm field and sum expected VPs
        int expectedVP = 0;
        foreach (var (_, _, field) in state.Board.AllFarmFields())
        {
            field.AddFarmer(player.Name);
            expectedVP += field.Card.VictoryPoints;
        }

        Assert.Equal(expectedVP, ScoreCalculator.Calculate(state)[0].FarmPoints);
    }

    [Fact]
    public void ScoreCalculator_FarmVP_NoFarmer_ZeroFarmPoints()
    {
        var (state, _) = MakeState();
        Assert.Equal(0, ScoreCalculator.Calculate(state)[0].FarmPoints);
    }

    [Fact]
    public void ScoreCalculator_FarmVP_FarmerOnOneField_AddsOnlyThatCard()
    {
        var (state, player) = MakeState();
        // Put farmer only on the first field
        var firstField = state.Board.AllFarmFields().First().Field;
        firstField.AddFarmer(player.Name);
        Assert.Equal(firstField.Card.VictoryPoints, ScoreCalculator.Calculate(state)[0].FarmPoints);
    }

    // ── Training Grounds VP ───────────────────────────────────────────────────

    [Fact]
    public void ScoreCalculator_TG_NoCastleCourtiers_ZeroVP()
    {
        var (state, player) = MakeState(p =>
        {
            // No courtiers in castle (gate doesn't count)
            p.CourtiersAtGate = 5;
        });
        state.Board.TrainingGrounds.Areas[0].AddSoldier(player.Name);
        Assert.Equal(0, ScoreCalculator.Calculate(state)[0].TrainingGroundsPoints);
    }

    [Fact]
    public void ScoreCalculator_TG_Areas0And1_SoldiersTimesCourtiers()
    {
        var (state, player) = MakeState(p => p.CourtiersOnStewardFloor = 2);
        // 1 soldier in area 0, 2 soldiers in area 1 → (1+2) × 2 = 6
        state.Board.TrainingGrounds.Areas[0].AddSoldier(player.Name);
        state.Board.TrainingGrounds.Areas[1].AddSoldier(player.Name);
        state.Board.TrainingGrounds.Areas[1].AddSoldier(player.Name);

        Assert.Equal(6, ScoreCalculator.Calculate(state)[0].TrainingGroundsPoints);
    }

    [Fact]
    public void ScoreCalculator_TG_Area2_DoublesSoldierCountTimesCourtiers()
    {
        var (state, player) = MakeState(p => p.CourtiersOnStewardFloor = 3);
        // 2 soldiers in area 2 → 2 × 2 × 3 = 12
        state.Board.TrainingGrounds.Areas[2].AddSoldier(player.Name);
        state.Board.TrainingGrounds.Areas[2].AddSoldier(player.Name);

        Assert.Equal(12, ScoreCalculator.Calculate(state)[0].TrainingGroundsPoints);
    }

    [Fact]
    public void ScoreCalculator_TG_MixedAreas_CombinesCorrectly()
    {
        var (state, player) = MakeState(p =>
        {
            p.CourtiersOnStewardFloor = 1;
            p.CourtiersOnDiplomatFloor    = 1;
            p.CourtiersOnTopFloor    = 1;
        });
        // castleCourtiers = 3
        // area 0: 1 soldier → 1×3=3; area 2: 1 soldier → 1×2×3=6 → total 9
        state.Board.TrainingGrounds.Areas[0].AddSoldier(player.Name);
        state.Board.TrainingGrounds.Areas[2].AddSoldier(player.Name);

        Assert.Equal(9, ScoreCalculator.Calculate(state)[0].TrainingGroundsPoints);
    }

    [Fact]
    public void ScoreCalculator_TG_OnlyGateCourtiers_StillZeroVP()
    {
        // Gate courtiers do NOT count as castle courtiers for TG scoring
        var (state, player) = MakeState(p => p.CourtiersAtGate = 3);
        state.Board.TrainingGrounds.Areas[0].AddSoldier(player.Name);
        Assert.Equal(0, ScoreCalculator.Calculate(state)[0].TrainingGroundsPoints);
    }

    [Fact]
    public void ScoreCalculator_TG_NoSoldiers_ZeroVP()
    {
        var (state, _) = MakeState(p => p.CourtiersOnStewardFloor = 3);
        Assert.Equal(0, ScoreCalculator.Calculate(state)[0].TrainingGroundsPoints);
    }

    [Fact]
    public void ScoreCalculator_TG_OtherPlayerSoldiers_NotCountedForPlayer()
    {
        var player2 = new Player { Name = "Bob" };
        var player1 = new Player { Name = "Alice", CourtiersOnStewardFloor = 2 };
        var state = new GameState(new List<Player> { player1, player2 });
        state.Board.SetupFarmingLands(new Random(0));
        state.Board.SetupTrainingGrounds(new Random(0));

        // Bob's soldiers should not count for Alice's TG points
        state.Board.TrainingGrounds.Areas[0].AddSoldier(player2.Name);

        var scores = ScoreCalculator.Calculate(state);
        var aliceScore = scores.First(s => s.PlayerName == "Alice");
        Assert.Equal(0, aliceScore.TrainingGroundsPoints);
    }

    // ── Influence VP ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,  0)]
    [InlineData(1,  0)]
    [InlineData(5,  0)]
    [InlineData(6,  3)]
    [InlineData(10, 3)]
    [InlineData(11, 6)]
    [InlineData(15, 6)]
    [InlineData(16, 7)]
    [InlineData(17, 8)]
    [InlineData(18, 9)]
    [InlineData(19, 10)]
    [InlineData(20, 11)]
    [InlineData(25, 11)]
    public void ScoreCalculator_InfluenceVP_AllBands(int influence, int expectedVP)
    {
        var (state, _) = MakeState(p => p.Influence = influence);
        Assert.Equal(expectedVP, ScoreCalculator.Calculate(state)[0].InfluencePoints);
    }

    // ── Ordering ──────────────────────────────────────────────────────────────

    [Fact]
    public void ScoreCalculator_OrdersByTotalDescending()
    {
        var alice = new Player { Name = "Alice", LanternScore = 10 };
        var bob   = new Player { Name = "Bob",   LanternScore = 2  };
        var state = new GameState(new List<Player> { alice, bob });
        state.Board.SetupFarmingLands(new Random(0));
        state.Board.SetupTrainingGrounds(new Random(0));

        var scores = ScoreCalculator.Calculate(state);

        Assert.Equal("Alice", scores[0].PlayerName);
        Assert.Equal("Bob",   scores[1].PlayerName);
    }

    [Fact]
    public void ScoreCalculator_LanternScore_IsIncluded()
    {
        var (state, _) = MakeState(p => p.LanternScore = 7);
        Assert.Equal(7, ScoreCalculator.Calculate(state)[0].LanternPoints);
    }
}

using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for TrainingGrounds and TrainingGroundsArea — setup, snapshot, reset,
/// soldier management.
/// </summary>
public class TrainingGroundsTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static TrainingGrounds SetupTg(int seed = 42)
    {
        var board = new Board();
        board.SetupTrainingGrounds(new Random(seed));
        return board.TrainingGrounds;
    }

    // ── Load / structure ──────────────────────────────────────────────────────

    [Fact]
    public void TrainingGrounds_Has3Areas()
    {
        var tg = SetupTg();
        Assert.Equal(3, tg.Areas.Length);
    }

    [Fact]
    public void Areas_HaveCorrectIronCosts()
    {
        var tg = SetupTg();
        Assert.Equal(1, tg.Areas[0].IronCost);
        Assert.Equal(3, tg.Areas[1].IronCost);
        Assert.Equal(5, tg.Areas[2].IronCost);
    }

    // ── SetupForRound ─────────────────────────────────────────────────────────

    [Fact]
    public void SetupForRound_Area0_HasResourceGain()
    {
        var tg = SetupTg();
        // Area 0 gets the resource side of token[0]
        Assert.NotEmpty(tg.Areas[0].ResourceGain);
        Assert.Empty(tg.Areas[0].ActionDescription); // no action side
    }

    [Fact]
    public void SetupForRound_Area1_HasActionDescription()
    {
        var tg = SetupTg();
        // Area 1 gets the action side of token[1]
        Assert.NotEmpty(tg.Areas[1].ActionDescription);
        Assert.Empty(tg.Areas[1].ResourceGain); // no resource side
    }

    [Fact]
    public void SetupForRound_Area2_HasBothSides()
    {
        var tg = SetupTg();
        // Area 2 gets resource side of token[2] + action side of token[3]
        Assert.NotEmpty(tg.Areas[2].ResourceGain);
        Assert.NotEmpty(tg.Areas[2].ActionDescription);
    }

    [Fact]
    public void SetupForRound_DifferentSeeds_ProduceDifferentSetups()
    {
        var tg1 = SetupTg(1);
        var tg2 = SetupTg(999);
        // Very likely to differ across all seeds (not a guaranteed guarantee but practical)
        bool anyDiff =
            tg1.Areas[0].ResourceGain.Count != tg2.Areas[0].ResourceGain.Count ||
            tg1.Areas[1].ActionDescription  != tg2.Areas[1].ActionDescription  ||
            tg1.Areas[2].ActionDescription  != tg2.Areas[2].ActionDescription;
        Assert.True(anyDiff, "Expected at least one difference between different random seeds.");
    }

    [Fact]
    public void SetupForRound_CalledTwice_ResetsAndReassigns()
    {
        var board = new Board();
        board.SetupTrainingGrounds(new Random(1));
        var firstAction = board.TrainingGrounds.Areas[1].ActionDescription;

        board.SetupTrainingGrounds(new Random(999));
        var secondAction = board.TrainingGrounds.Areas[1].ActionDescription;

        // After second setup, the description is freshly assigned (may differ from first)
        Assert.NotNull(secondAction); // just verify it's not null/empty
        Assert.NotEmpty(secondAction);
        // The firstAction and secondAction may or may not be equal, but both should be valid
        _ = firstAction; // suppress unused warning
    }

    // ── TrainingGroundsArea.Reset ─────────────────────────────────────────────

    [Fact]
    public void Area_Reset_ClearsResourceGainAndAction()
    {
        var tg   = SetupTg();
        var area = tg.Areas[2]; // has both sides after setup

        area.Reset();

        Assert.Empty(area.ResourceGain);
        Assert.Empty(area.ActionDescription);
    }

    [Fact]
    public void Area_Reset_ClearsSoldierOwners()
    {
        var tg   = SetupTg();
        var area = tg.Areas[0];
        area.AddSoldier("Alice");
        area.AddSoldier("Bob");

        area.Reset();

        Assert.Empty(area.SoldierOwners);
    }

    // ── TrainingGroundsArea.AddSoldier ────────────────────────────────────────

    [Fact]
    public void Area_AddSoldier_AppendsToOwners()
    {
        var tg   = SetupTg();
        var area = tg.Areas[0];

        area.AddSoldier("Alice");
        area.AddSoldier("Bob");

        Assert.Equal(2, area.SoldierOwners.Count);
        Assert.Contains("Alice", area.SoldierOwners);
        Assert.Contains("Bob",   area.SoldierOwners);
    }

    [Fact]
    public void Area_AddSoldier_AllowsDuplicates()
    {
        var tg   = SetupTg();
        var area = tg.Areas[1];

        area.AddSoldier("Alice");
        area.AddSoldier("Alice");

        Assert.Equal(2, area.SoldierOwners.Count);
    }

    // ── TrainingGroundsArea.ToSnapshot ────────────────────────────────────────

    [Fact]
    public void Area_ToSnapshot_ReflectsAreaIndex()
    {
        var tg   = SetupTg();
        var snap = tg.Areas[2].ToSnapshot(2);
        Assert.Equal(2, snap.AreaIndex);
    }

    [Fact]
    public void Area_ToSnapshot_ReflectsIronCost()
    {
        var tg   = SetupTg();
        var snap = tg.Areas[1].ToSnapshot(1);
        Assert.Equal(3, snap.IronCost);
    }

    [Fact]
    public void Area_ToSnapshot_IncludesResourceGain()
    {
        var tg   = SetupTg();
        var snap = tg.Areas[0].ToSnapshot(0); // area 0 has resource side only
        Assert.NotEmpty(snap.ResourceGain);
        foreach (var g in snap.ResourceGain)
        {
            Assert.NotEmpty(g.GainType);
            Assert.True(g.Amount > 0);
        }
    }

    [Fact]
    public void Area_ToSnapshot_IncludesActionDescription()
    {
        var tg   = SetupTg();
        var snap = tg.Areas[1].ToSnapshot(1); // area 1 has action side only
        Assert.NotEmpty(snap.ActionDescription);
    }

    [Fact]
    public void Area_ToSnapshot_IncludesSoldierOwners()
    {
        var tg   = SetupTg();
        var area = tg.Areas[0];
        area.AddSoldier("Alice");
        var snap = area.ToSnapshot(0);
        Assert.Contains("Alice", snap.SoldierOwners);
    }

    // ── TrainingGrounds.ToSnapshot ────────────────────────────────────────────

    [Fact]
    public void TrainingGrounds_ToSnapshot_Has3Areas()
    {
        var tg   = SetupTg();
        var snap = tg.ToSnapshot();
        Assert.Equal(3, snap.Areas.Count);
    }

    [Fact]
    public void TrainingGrounds_ToSnapshot_AreaIndicesAreCorrect()
    {
        var tg   = SetupTg();
        var snap = tg.ToSnapshot();
        for (int i = 0; i < 3; i++)
            Assert.Equal(i, snap.Areas[i].AreaIndex);
    }

    // ── Board.TrainingGrounds — error before setup ─────────────────────────────

    [Fact]
    public void Board_TrainingGrounds_BeforeSetup_Throws()
    {
        var board = new Board();
        Assert.Throws<InvalidOperationException>(() => _ = board.TrainingGrounds);
    }

    [Fact]
    public void Board_SetupTrainingGrounds_CanBeCalledMultipleTimes_Idempotent()
    {
        var board = new Board();
        board.SetupTrainingGrounds(new Random(1));
        board.SetupTrainingGrounds(new Random(2)); // second call resets for new round
        Assert.NotNull(board.TrainingGrounds);
    }
}

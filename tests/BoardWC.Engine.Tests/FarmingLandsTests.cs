using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for FarmingLands and FarmField — GetField ColorIndex dispatch,
/// AllFields enumeration, HasFarmer/AddFarmer, and ToSnapshot.
/// </summary>
public class FarmingLandsTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static FarmingLands SetupLands(int seed = 42)
    {
        var board = new Board();
        board.SetupFarmingLands(new Random(seed));
        return board.FarmingLands;
    }

    // ── GetField — ColorIndex dispatch (Red=0, Black=1, White=2) ─────────────

    [Fact]
    public void GetField_Red_Inland_ReturnsField()
    {
        var fl    = SetupLands();
        var field = fl.GetField(BridgeColor.Red, true);
        Assert.NotNull(field);
        Assert.NotNull(field.Card);
    }

    [Fact]
    public void GetField_Red_Outside_ReturnsDistinctField()
    {
        var fl      = SetupLands();
        var inland  = fl.GetField(BridgeColor.Red, true);
        var outside = fl.GetField(BridgeColor.Red, false);
        // Inland and outside come from different decks — should be different objects
        Assert.NotSame(inland, outside);
    }

    [Fact]
    public void GetField_Black_Inland_ReturnsField()
    {
        var fl    = SetupLands();
        var field = fl.GetField(BridgeColor.Black, true);
        Assert.NotNull(field);
        Assert.NotNull(field.Card);
    }

    [Fact]
    public void GetField_Black_Outside_ReturnsDistinctField()
    {
        var fl      = SetupLands();
        var inland  = fl.GetField(BridgeColor.Black, true);
        var outside = fl.GetField(BridgeColor.Black, false);
        Assert.NotSame(inland, outside);
    }

    [Fact]
    public void GetField_White_Inland_ReturnsField()
    {
        var fl    = SetupLands();
        var field = fl.GetField(BridgeColor.White, true);
        Assert.NotNull(field);
        Assert.NotNull(field.Card);
    }

    [Fact]
    public void GetField_White_Outside_ReturnsDistinctField()
    {
        var fl      = SetupLands();
        var inland  = fl.GetField(BridgeColor.White, true);
        var outside = fl.GetField(BridgeColor.White, false);
        Assert.NotSame(inland, outside);
    }

    [Fact]
    public void GetField_ThreeColors_ReturnDifferentFields()
    {
        var fl    = SetupLands();
        var red   = fl.GetField(BridgeColor.Red,   true);
        var black = fl.GetField(BridgeColor.Black, true);
        var white = fl.GetField(BridgeColor.White, true);
        // All three inland fields come from different card slots
        Assert.NotSame(red, black);
        Assert.NotSame(red, white);
        Assert.NotSame(black, white);
    }

    // ── AllFields ─────────────────────────────────────────────────────────────

    [Fact]
    public void AllFields_Returns6Fields()
    {
        var fl = SetupLands();
        Assert.Equal(6, fl.AllFields().Count());
    }

    [Fact]
    public void AllFields_ContainsAllColors()
    {
        var fl     = SetupLands();
        var colors = fl.AllFields().Select(f => f.Color).ToList();
        Assert.Contains(BridgeColor.Red,   colors);
        Assert.Contains(BridgeColor.Black, colors);
        Assert.Contains(BridgeColor.White, colors);
    }

    [Fact]
    public void AllFields_EachColorAppearsAsInlandAndOutside()
    {
        var fl = SetupLands();
        foreach (var color in new[] { BridgeColor.Red, BridgeColor.Black, BridgeColor.White })
        {
            var forColor = fl.AllFields().Where(f => f.Color == color).ToList();
            Assert.Equal(2, forColor.Count);
            Assert.Contains(forColor, f => f.IsInland == true);
            Assert.Contains(forColor, f => f.IsInland == false);
        }
    }

    [Fact]
    public void AllFields_GetField_Consistent()
    {
        // AllFields() and GetField() must return the same FarmField objects
        var fl = SetupLands();
        foreach (var (color, isInland, field) in fl.AllFields())
            Assert.Same(fl.GetField(color, isInland), field);
    }

    // ── HasFarmer / AddFarmer ─────────────────────────────────────────────────

    [Fact]
    public void HasFarmer_Initially_ReturnsFalse()
    {
        var fl    = SetupLands();
        var field = fl.GetField(BridgeColor.Red, true);
        Assert.False(field.HasFarmer("Alice"));
    }

    [Fact]
    public void AddFarmer_ThenHasFarmer_ReturnsTrue()
    {
        var fl    = SetupLands();
        var field = fl.GetField(BridgeColor.Red, true);
        field.AddFarmer("Alice");
        Assert.True(field.HasFarmer("Alice"));
    }

    [Fact]
    public void HasFarmer_OtherPlayer_ReturnsFalse()
    {
        var fl    = SetupLands();
        var field = fl.GetField(BridgeColor.Red, true);
        field.AddFarmer("Alice");
        Assert.False(field.HasFarmer("Bob"));
    }

    [Fact]
    public void AddFarmer_AllowsDuplicates()
    {
        var fl    = SetupLands();
        var field = fl.GetField(BridgeColor.Red, true);
        field.AddFarmer("Alice");
        field.AddFarmer("Alice");
        Assert.Equal(2, field.FarmerOwners.Count);
    }

    [Fact]
    public void AddFarmer_MultipleOwners_AllPresent()
    {
        var fl    = SetupLands();
        var field = fl.GetField(BridgeColor.Black, false);
        field.AddFarmer("Alice");
        field.AddFarmer("Bob");
        Assert.Contains("Alice", field.FarmerOwners);
        Assert.Contains("Bob",   field.FarmerOwners);
    }

    // ── FarmField.ToSnapshot ─────────────────────────────────────────────────

    [Fact]
    public void ToSnapshot_ReflectsColor()
    {
        var fl   = SetupLands();
        var snap = fl.GetField(BridgeColor.White, true).ToSnapshot(BridgeColor.White, true);
        Assert.Equal(BridgeColor.White, snap.BridgeColor);
    }

    [Fact]
    public void ToSnapshot_ReflectsIsInland()
    {
        var fl   = SetupLands();
        var snap = fl.GetField(BridgeColor.Red, false).ToSnapshot(BridgeColor.Red, false);
        Assert.False(snap.IsInland);
    }

    [Fact]
    public void ToSnapshot_FoodCostIsNonNegative()
    {
        var fl   = SetupLands();
        var snap = fl.GetField(BridgeColor.Red, true).ToSnapshot(BridgeColor.Red, true);
        Assert.True(snap.FoodCost >= 0);
    }

    [Fact]
    public void ToSnapshot_GainItemsNotNull()
    {
        var fl   = SetupLands();
        var snap = fl.GetField(BridgeColor.Black, true).ToSnapshot(BridgeColor.Black, true);
        Assert.NotNull(snap.GainItems);
    }

    [Fact]
    public void ToSnapshot_FarmerOwners_ReflectsAddedFarmers()
    {
        var fl    = SetupLands();
        var field = fl.GetField(BridgeColor.White, false);
        field.AddFarmer("Charlie");
        var snap = field.ToSnapshot(BridgeColor.White, false);
        Assert.Contains("Charlie", snap.FarmerOwners);
    }

    [Fact]
    public void ToSnapshot_NoFarmers_FarmerOwnersEmpty()
    {
        var fl   = SetupLands();
        var snap = fl.GetField(BridgeColor.Red, true).ToSnapshot(BridgeColor.Red, true);
        Assert.Empty(snap.FarmerOwners);
    }

    // ── FarmingLands.ToSnapshot ───────────────────────────────────────────────

    [Fact]
    public void FarmingLandsSnapshot_Has6Fields()
    {
        var fl   = SetupLands();
        var snap = fl.ToSnapshot();
        Assert.Equal(6, snap.Fields.Count);
    }

    [Fact]
    public void FarmingLandsSnapshot_ContainsAllColors()
    {
        var fl     = SetupLands();
        var snap   = fl.ToSnapshot();
        var colors = snap.Fields.Select(f => f.BridgeColor).ToList();
        Assert.Contains(BridgeColor.Red,   colors);
        Assert.Contains(BridgeColor.Black, colors);
        Assert.Contains(BridgeColor.White, colors);
    }

    // ── Board.FarmingLands — error before setup ───────────────────────────────

    [Fact]
    public void Board_FarmingLands_BeforeSetup_Throws()
    {
        var board = new Board();
        Assert.Throws<InvalidOperationException>(() => _ = board.FarmingLands);
    }

    [Fact]
    public void Board_SetupFarmingLands_CalledTwice_IsIdempotent()
    {
        // Second call should not reset (farmers persist) — just verifies no throw
        var board = new Board();
        board.SetupFarmingLands(new Random(1));
        var firstCard = board.FarmingLands.GetField(BridgeColor.Red, true).Card;
        board.SetupFarmingLands(new Random(2));
        // Card is unchanged (second call is a no-op)
        Assert.Same(firstCard, board.FarmingLands.GetField(BridgeColor.Red, true).Card);
    }
}

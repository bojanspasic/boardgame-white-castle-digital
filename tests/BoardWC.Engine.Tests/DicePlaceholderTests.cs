using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for DicePlaceholder.CanAccept and GetCompareValue.
/// </summary>
public class DicePlaceholderTests
{
    // ── CanAccept ─────────────────────────────────────────────────────────────

    [Fact]
    public void CanAccept_UnlimitedCapacity_AlwaysTrue()
    {
        var ph = new DicePlaceholder(1, unlimitedCapacity: true);
        // Place many dice — still accepts more
        for (int i = 0; i < 10; i++)
            ph.PlaceDie(new Die(3, BridgeColor.Red));

        Assert.True(ph.CanAccept(2));
        Assert.True(ph.CanAccept(4));
    }

    [Fact]
    public void CanAccept_TwoPlayerGame_EmptySlot_ReturnsTrue()
    {
        var ph = new DicePlaceholder(3);
        Assert.True(ph.CanAccept(2));
    }

    [Fact]
    public void CanAccept_TwoPlayerGame_OneOccupant_ReturnsFalse()
    {
        var ph = new DicePlaceholder(3);
        ph.PlaceDie(new Die(4, BridgeColor.Black));

        Assert.False(ph.CanAccept(2));
    }

    [Fact]
    public void CanAccept_ThreePlayerGame_EmptySlot_ReturnsTrue()
    {
        var ph = new DicePlaceholder(3);
        Assert.True(ph.CanAccept(3));
    }

    [Fact]
    public void CanAccept_ThreePlayerGame_OneOccupant_ReturnsTrue()
    {
        var ph = new DicePlaceholder(3);
        ph.PlaceDie(new Die(4, BridgeColor.Black));

        Assert.True(ph.CanAccept(3));
    }

    [Fact]
    public void CanAccept_ThreePlayerGame_TwoOccupants_ReturnsFalse()
    {
        var ph = new DicePlaceholder(3);
        ph.PlaceDie(new Die(4, BridgeColor.Black));
        ph.PlaceDie(new Die(5, BridgeColor.Red));

        Assert.False(ph.CanAccept(3));
    }

    [Fact]
    public void CanAccept_FourPlayerGame_OneOccupant_ReturnsTrue()
    {
        var ph = new DicePlaceholder(3);
        ph.PlaceDie(new Die(4, BridgeColor.Black));

        Assert.True(ph.CanAccept(4));
    }

    [Fact]
    public void CanAccept_FourPlayerGame_TwoOccupants_ReturnsFalse()
    {
        var ph = new DicePlaceholder(3);
        ph.PlaceDie(new Die(4, BridgeColor.Black));
        ph.PlaceDie(new Die(5, BridgeColor.Red));

        Assert.False(ph.CanAccept(4));
    }

    // ── GetCompareValue ───────────────────────────────────────────────────────

    [Fact]
    public void GetCompareValue_EmptySlot_ReturnsBaseValue()
    {
        var ph = new DicePlaceholder(5);
        Assert.Equal(5, ph.GetCompareValue(3));
    }

    [Fact]
    public void GetCompareValue_TwoPlayerGame_IgnoresPlacedDie_ReturnsBaseValue()
    {
        var ph = new DicePlaceholder(5);
        ph.PlaceDie(new Die(2, BridgeColor.White));

        // 2-player: always compare to base value, never the placed die
        Assert.Equal(5, ph.GetCompareValue(2));
    }

    [Fact]
    public void GetCompareValue_ThreePlayerGame_SecondDieComparesToFirst()
    {
        var ph = new DicePlaceholder(5);
        ph.PlaceDie(new Die(3, BridgeColor.Black)); // first die placed

        // 3+ players: second die compares against the already-placed die's value
        Assert.Equal(3, ph.GetCompareValue(3));
    }

    [Fact]
    public void GetCompareValue_FourPlayerGame_SecondDieComparesToFirst()
    {
        var ph = new DicePlaceholder(4);
        ph.PlaceDie(new Die(6, BridgeColor.Red));

        Assert.Equal(6, ph.GetCompareValue(4));
    }

    [Fact]
    public void GetCompareValue_UnlimitedCapacity_AlwaysReturnsBaseValue()
    {
        var ph = new DicePlaceholder(1, unlimitedCapacity: true);
        ph.PlaceDie(new Die(6, BridgeColor.Red));

        // Well: always compare to BaseValue regardless of player count or placed dice
        Assert.Equal(1, ph.GetCompareValue(4));
        Assert.Equal(1, ph.GetCompareValue(2));
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_RemovesAllPlacedDice()
    {
        var ph = new DicePlaceholder(3);
        ph.PlaceDie(new Die(4, BridgeColor.Red));
        ph.PlaceDie(new Die(5, BridgeColor.Black));

        ph.Clear();

        Assert.Empty(ph.PlacedDice);
        Assert.True(ph.CanAccept(2)); // slot open again
    }

    // ── ToSnapshot ────────────────────────────────────────────────────────────

    [Fact]
    public void ToSnapshot_ReflectsBaseValueAndCapacity()
    {
        var ph = new DicePlaceholder(7, unlimitedCapacity: true);
        var snap = ph.ToSnapshot();

        Assert.Equal(7, snap.BaseValue);
        Assert.True(snap.UnlimitedCapacity);
        Assert.Empty(snap.PlacedDice);
        Assert.Empty(snap.Tokens);
        Assert.Null(snap.Card);
    }

    [Fact]
    public void ToSnapshot_IncludesPlacedDice()
    {
        var ph = new DicePlaceholder(3);
        ph.PlaceDie(new Die(5, BridgeColor.White));
        var snap = ph.ToSnapshot();

        Assert.Single(snap.PlacedDice);
        Assert.Equal(5, snap.PlacedDice[0].Value);
        Assert.Equal(BridgeColor.White, snap.PlacedDice[0].Color);
    }

    [Fact]
    public void ToSnapshot_IncludesTokens()
    {
        var ph = new DicePlaceholder(3);
        ph.AddToken(new Token(BridgeColor.Red, TokenResource.Food));
        var snap = ph.ToSnapshot();

        Assert.Single(snap.Tokens);
        Assert.Equal(BridgeColor.Red, snap.Tokens[0].DieColor);
    }
}

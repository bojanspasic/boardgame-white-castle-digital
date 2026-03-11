using BoardWC.Console.UI;

namespace BoardWC.Console.Tests;

public class ConsoleBoxTests
{
    private const string P = "  "; // a sample indent prefix

    // ── HorizPad ──────────────────────────────────────────────────────────────

    [Fact]
    public void HorizPad_AiOverlayInner_Returns22()
    {
        // AiThinkingOverlay.Inner = 34; box width = 36; (80 - 36) / 2 = 22
        Assert.Equal(22, ConsoleBox.HorizPad(34));
    }

    [Fact]
    public void HorizPad_MainMenuBoxInner_Returns11()
    {
        // MainMenu.BoxInner = 56; box width = 58; (80 - 58) / 2 = 11
        Assert.Equal(11, ConsoleBox.HorizPad(56));
    }

    [Fact]
    public void HorizPad_AccountsForTwoBorderChars()
    {
        // Inner=76 → box width=78 → (80-78)/2=1; inner=77 → 78-79=-1 → 0
        Assert.Equal(1, ConsoleBox.HorizPad(76));
        Assert.Equal(0, ConsoleBox.HorizPad(77));
    }

    [Fact]
    public void HorizPad_ReturnsZero_WhenBoxExceedsDisplay()
    {
        Assert.Equal(0, ConsoleBox.HorizPad(80));
    }

    // ── VertPad ───────────────────────────────────────────────────────────────

    [Fact]
    public void VertPad_StandardWindow_ReturnsCorrectPad()
    {
        // WindowHeight=25, BoxLines=13 → (25-13)/2=6
        Assert.Equal(6, ConsoleBox.VertPad(25, 13));
    }

    [Fact]
    public void VertPad_SixLineBox_ReturnsHalf()
    {
        // WindowHeight=16, BoxLines=6 → (16-6)/2=5
        Assert.Equal(5, ConsoleBox.VertPad(16, 6));
    }

    [Fact]
    public void VertPad_ReturnsZero_WhenBoxTooTall()
    {
        Assert.Equal(0, ConsoleBox.VertPad(4, 13));
    }

    [Fact]
    public void VertPad_OddRemainder_FloorDivision()
    {
        // (9 - 6) / 2 = 1 (not 1.5)
        Assert.Equal(1, ConsoleBox.VertPad(9, 6));
    }

    // ── TopBorder ─────────────────────────────────────────────────────────────

    [Fact]
    public void TopBorder_StartsWithPrefixAndCorners()
    {
        string row = ConsoleBox.TopBorder(P, 4);
        Assert.StartsWith(P + "╔", row);
        Assert.EndsWith("╗", row);
    }

    [Fact]
    public void TopBorder_HorizontalCharCount()
    {
        string row = ConsoleBox.TopBorder(P, 10);
        Assert.Equal(10, row.Count(c => c == '═'));
    }

    // ── BlankRow ──────────────────────────────────────────────────────────────

    [Fact]
    public void BlankRow_HasBordersAndSpaces()
    {
        string row = ConsoleBox.BlankRow(P, 5);
        Assert.Contains("║     ║", row);
    }

    [Fact]
    public void BlankRow_SpaceCount()
    {
        string row = ConsoleBox.BlankRow("", 8);
        int spaces = row.Count(c => c == ' ');
        Assert.Equal(8, spaces);
    }

    [Fact]
    public void BlankRow_NoShadow()
    {
        string row = ConsoleBox.BlankRow(P, 5);
        Assert.DoesNotContain("▒", row);
    }

    // ── BlankRowShadow ────────────────────────────────────────────────────────

    [Fact]
    public void BlankRowShadow_EndsWithShadow()
    {
        string row = ConsoleBox.BlankRowShadow(P, 5);
        Assert.EndsWith("║▒", row);
    }

    [Fact]
    public void BlankRowShadow_SpaceCount()
    {
        string row = ConsoleBox.BlankRowShadow("", 6);
        // 6 spaces between the two ║ characters
        int bar1 = row.IndexOf('║');
        int bar2 = row.LastIndexOf('║');
        Assert.Equal(6, bar2 - bar1 - 1);
    }

    // ── ContentRowShadow ──────────────────────────────────────────────────────

    [Fact]
    public void ContentRowShadow_IncludesContent()
    {
        string row = ConsoleBox.ContentRowShadow(P, 10, "HELLO");
        Assert.Contains("HELLO", row);
    }

    [Fact]
    public void ContentRowShadow_PadsContentToInnerWidth()
    {
        string row = ConsoleBox.ContentRowShadow("", 10, "HI"); // "HI" is 2 chars, padded to 10
        int bar1 = row.IndexOf('║');
        int bar2 = row.LastIndexOf('║');
        Assert.Equal(10, bar2 - bar1 - 1);
    }

    [Fact]
    public void ContentRowShadow_EndsWithBorderAndShadow()
    {
        string row = ConsoleBox.ContentRowShadow(P, 5, "X");
        Assert.EndsWith("║▒", row);
    }

    [Fact]
    public void ContentRowShadow_StartsWithPrefixAndBorder()
    {
        string row = ConsoleBox.ContentRowShadow(P, 5, "X");
        Assert.StartsWith(P + "║", row);
    }

    // ── BottomBorder ──────────────────────────────────────────────────────────

    [Fact]
    public void BottomBorder_StartsWithPrefixAndCorner()
    {
        string row = ConsoleBox.BottomBorder(P, 5);
        Assert.StartsWith(P + "╚", row);
    }

    [Fact]
    public void BottomBorder_EndsWithCornerAndShadow()
    {
        string row = ConsoleBox.BottomBorder(P, 5);
        Assert.EndsWith("╝▒", row);
    }

    [Fact]
    public void BottomBorder_HorizontalCharCount()
    {
        string row = ConsoleBox.BottomBorder(P, 12);
        Assert.Equal(12, row.Count(c => c == '═'));
    }

    // ── ShadowLine ────────────────────────────────────────────────────────────

    [Fact]
    public void ShadowLine_ShadowCharCount_IsInnerPlusTwo()
    {
        string row = ConsoleBox.ShadowLine("", 10);
        Assert.Equal(12, row.Count(c => c == '▒'));
    }

    [Fact]
    public void ShadowLine_StartsWithOneSpaceOffset()
    {
        string row = ConsoleBox.ShadowLine("", 5);
        Assert.Equal(' ', row[0]);
        Assert.Equal('▒', row[1]);
    }

    [Fact]
    public void ShadowLine_PrefixPlusOneSpaceThenShadow()
    {
        string row = ConsoleBox.ShadowLine(P, 4);
        Assert.StartsWith(P + " ▒", row);
    }

    // ── Center ────────────────────────────────────────────────────────────────

    [Fact]
    public void Center_ResultLengthEqualsInner()
    {
        string result = ConsoleBox.Center("HI", 20);
        Assert.Equal(20, result.Length);
    }

    [Fact]
    public void Center_TextIsCenteredWithLeadingSpaces()
    {
        // "HI" (2 chars) in inner=10 → leftPad=(10+2)/2 - 2 = 4
        string result = ConsoleBox.Center("HI", 10);
        Assert.Equal(4, result.IndexOf('H'));
    }

    [Fact]
    public void Center_EvenRemainder_PaddedSymmetrically()
    {
        // "AB" (2 chars) in inner=6 → leftPad=2, rightPad=2
        string result = ConsoleBox.Center("AB", 6);
        Assert.Equal("  AB  ", result);
    }

    [Fact]
    public void Center_OddRemainder_ExtraSpaceOnRight()
    {
        // "A" (1 char) in inner=4 → leftPad=(4+1)/2 - 1 = 1, rightPad=3
        string result = ConsoleBox.Center("A", 4);
        Assert.Equal(" A  ", result);
    }

    [Fact]
    public void Center_TextFillsInner_NoExtraPadding()
    {
        string result = ConsoleBox.Center("HELLO", 5);
        Assert.Equal("HELLO", result);
    }
}

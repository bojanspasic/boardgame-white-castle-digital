using BoardWC.Console.UI;

namespace BoardWC.Console.Tests;

/// <summary>
/// Unit tests for AiThinkingOverlay — spinner, box structure, shadow, centering, and Show.
/// </summary>
public class AiThinkingOverlayTests
{
    // ── Fake console ──────────────────────────────────────────────────────────

    private sealed class FakeConsole : IConsoleIO
    {
        public int WindowWidth  { get; set; } = 80;
        public int WindowHeight { get; set; } = 25;
        public (int Left, int Top)? LastCursorPosition { get; private set; }
        public List<string> Written { get; } = new();
        public List<(string Text, ConsoleColor Color)> Colored { get; } = new();

        public void SetCursorPosition(int left, int top) => LastCursorPosition = (left, top);
        public void Clear() { }
        public void Write(string text)                        => Written.Add(text);
        public void WriteColored(string text, ConsoleColor c) => Colored.Add((text, c));
        public void WriteLine(string text)                    => Written.Add(text + "\n");
        public ConsoleKeyInfo ReadKey(bool intercept)         => throw new NotSupportedException();
    }

    // Helper: extract the inner content (between ║ chars) from a written line
    private static string ExtractInner(string line)
    {
        string trimmed = line.TrimEnd('\n').Trim();
        int first = trimmed.IndexOf('║');
        int last  = trimmed.LastIndexOf('║');
        return trimmed.Substring(first + 1, last - first - 1);
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Fact]
    public void MessageText_IsExpectedString()
    {
        Assert.Equal("AI player is thinking", AiThinkingOverlay.MessageText);
    }

    [Fact]
    public void Inner_Is34()
    {
        Assert.Equal(34, AiThinkingOverlay.Inner);
    }

    [Fact]
    public void SpinnerFrames_HasFourElements()
    {
        Assert.Equal(4, AiThinkingOverlay.SpinnerFrames.Length);
    }

    // ── GetSpinnerChar ────────────────────────────────────────────────────────

    [Fact]
    public void GetSpinnerChar_Frame0_ReturnsFirstChar()
    {
        Assert.Equal(AiThinkingOverlay.SpinnerFrames[0], AiThinkingOverlay.GetSpinnerChar(0));
    }

    [Fact]
    public void GetSpinnerChar_Frame1_ReturnsSecondChar()
    {
        Assert.Equal(AiThinkingOverlay.SpinnerFrames[1], AiThinkingOverlay.GetSpinnerChar(1));
    }

    [Fact]
    public void GetSpinnerChar_Frame2_ReturnsThirdChar()
    {
        Assert.Equal(AiThinkingOverlay.SpinnerFrames[2], AiThinkingOverlay.GetSpinnerChar(2));
    }

    [Fact]
    public void GetSpinnerChar_Frame3_ReturnsFourthChar()
    {
        Assert.Equal(AiThinkingOverlay.SpinnerFrames[3], AiThinkingOverlay.GetSpinnerChar(3));
    }

    [Fact]
    public void GetSpinnerChar_Frame4_WrapsToFirstChar()
    {
        Assert.Equal(AiThinkingOverlay.SpinnerFrames[0], AiThinkingOverlay.GetSpinnerChar(4));
    }

    [Fact]
    public void GetSpinnerChar_LargeFrame_WrapsCorrectly()
    {
        Assert.Equal(AiThinkingOverlay.SpinnerFrames[2], AiThinkingOverlay.GetSpinnerChar(102));
    }

    // ── RenderFrame — cursor positioning (replaces Clear) ────────────────────

    [Fact]
    public void RenderFrame_SetsCursorToLeftZero()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(0, console.LastCursorPosition!.Value.Left);
    }

    [Fact]
    public void RenderFrame_SetsCursorRowToTopPad_TallWindow()
    {
        // WindowHeight=16 → topPad=(16-6)/2=5
        var console = new FakeConsole { WindowHeight = 16 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(5, console.LastCursorPosition!.Value.Top);
    }

    [Fact]
    public void RenderFrame_SetsCursorRowToZero_SmallWindow()
    {
        // WindowHeight=4 (< 6) → topPad=0
        var console = new FakeConsole { WindowHeight = 4 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(0, console.LastCursorPosition!.Value.Top);
    }

    [Fact]
    public void RenderFrame_SetsCursorRowToTopPad_OddHeight()
    {
        // WindowHeight=9 → topPad=(9-6)/2=1; if constant 6 mutated to 5: topPad=2 (caught)
        var console = new FakeConsole { WindowHeight = 9 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(1, console.LastCursorPosition!.Value.Top);
    }

    // ── RenderFrame — written line count ──────────────────────────────────────

    [Fact]
    public void RenderFrame_WritesExactlyFiveNonColoredLines()
    {
        // top, blank, blankS, bottomS, shadowLn via WriteLine (textLn goes to WriteColored)
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(5, console.Written.Count);
    }

    [Fact]
    public void RenderFrame_WritesExactlyOneColoredLine()
    {
        // textLn is written via WriteColored
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(1, console.Colored.Count);
    }

    [Fact]
    public void RenderFrame_WrittenLineCountIsAlwaysFive_RegardlessOfHeight()
    {
        // SetCursorPosition handles vertical offset; no blank lines added to Written
        var console = new FakeConsole { WindowHeight = 25 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(5, console.Written.Count);
    }

    // ── RenderFrame — box drawing chars ──────────────────────────────────────

    [Fact]
    public void RenderFrame_ContainsTopLeftCorner()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Contains(console.Written, w => w.Contains('╔'));
    }

    [Fact]
    public void RenderFrame_ContainsTopRightCorner()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Contains(console.Written, w => w.Contains('╗'));
    }

    [Fact]
    public void RenderFrame_ContainsBottomLeftCorner()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Contains(console.Written, w => w.Contains('╚'));
    }

    [Fact]
    public void RenderFrame_ContainsBottomRightCorner()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Contains(console.Written, w => w.Contains('╝'));
    }

    [Fact]
    public void RenderFrame_ContainsHorizontalBorderChar()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Contains(console.Written, w => w.Contains('═'));
    }

    [Fact]
    public void RenderFrame_ContainsVerticalBorderChar()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0);
        // ║ appears in Written (blank, blankS) and in Colored (textLn)
        Assert.True(console.Written.Any(w => w.Contains('║'))
                 || console.Colored.Any(c => c.Text.Contains('║')));
    }

    // ── RenderFrame — shadow ──────────────────────────────────────────────────

    [Fact]
    public void RenderFrame_ContainsShadowChar()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0);
        bool inWritten = console.Written.Any(w => w.Contains('▒'));
        bool inColored = console.Colored.Any(c => c.Text.Contains('▒'));
        Assert.True(inWritten || inColored);
    }

    [Fact]
    public void RenderFrame_TwoWrittenRowsHaveShadowOnRight()
    {
        // blankS and bottomS are in Written and both end with ▒
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        int count = console.Written.Count(w => w.TrimEnd('\n').EndsWith('▒')
                                            && (w.Contains('║') || w.Contains('╝')));
        Assert.Equal(2, count);
    }

    [Fact]
    public void RenderFrame_ColoredTextLineHasShadowOnRight()
    {
        // textLn ends with ║▒ and is in Colored
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.True(console.Colored[0].Text.TrimEnd().EndsWith('▒'));
    }

    [Fact]
    public void RenderFrame_ShadowRow_HasCorrectShadowCharCount()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string shadowRow = console.Written.Last().TrimEnd('\n');
        Assert.Equal(AiThinkingOverlay.Inner + 2, shadowRow.Count(c => c == '▒'));
    }

    [Fact]
    public void RenderFrame_ShadowRow_HasOneSpaceOffsetFromBoxLeftEdge()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string topRow    = console.Written[0].TrimEnd('\n');
        string shadowRow = console.Written[4].TrimEnd('\n'); // last of 5 Written lines
        int boxLeft = topRow.IndexOf('╔');
        Assert.Equal(' ',  shadowRow[boxLeft]);
        Assert.Equal('▒', shadowRow[boxLeft + 1]);
    }

    // ── RenderFrame — top/bottom border structure ─────────────────────────────

    [Fact]
    public void RenderFrame_TopBorderHasCorrectHorizontalCount()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string top = console.Written[0].TrimEnd('\n').Trim();
        Assert.Equal(AiThinkingOverlay.Inner, top.Count(c => c == '═'));
    }

    [Fact]
    public void RenderFrame_TopBorderStartsWithCornerAndEndsWithCorner()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string top = console.Written[0].TrimEnd('\n').Trim();
        Assert.StartsWith("╔", top);
        Assert.EndsWith("╗", top);
    }

    [Fact]
    public void RenderFrame_BottomBorderHasCorrectHorizontalCount()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string bottom = console.Written[3].TrimEnd('\n').Trim(); // index 3 of 5 Written
        Assert.Equal(AiThinkingOverlay.Inner, bottom.Count(c => c == '═'));
    }

    [Fact]
    public void RenderFrame_BottomBorderStartsWithCornerAndEndsWithShadow()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string bottom = console.Written[3].TrimEnd('\n').Trim();
        Assert.StartsWith("╚", bottom);
        Assert.EndsWith("▒", bottom);
    }

    // ── RenderFrame — blank rows ──────────────────────────────────────────────

    [Fact]
    public void RenderFrame_FirstBlankRow_HasExactlyInnerSpacesBetweenBorders()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string blankRow = console.Written[1].TrimEnd('\n').Trim(); // Written[1] = blank
        int firstBar = blankRow.IndexOf('║');
        int lastBar  = blankRow.LastIndexOf('║');
        Assert.Equal(AiThinkingOverlay.Inner, lastBar - firstBar - 1);
    }

    [Fact]
    public void RenderFrame_ShadowBlankRow_HasExactlyInnerSpacesBetweenBorders()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string blankS = console.Written[2].TrimEnd('\n').Trim(); // Written[2] = blankS
        int firstBar = blankS.IndexOf('║');
        int lastBar  = blankS.LastIndexOf('║');
        Assert.Equal(AiThinkingOverlay.Inner, lastBar - firstBar - 1);
    }

    // ── RenderFrame — colored text line content & spacing ────────────────────

    [Fact]
    public void RenderFrame_TextLineContainsMessageText()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Contains(AiThinkingOverlay.MessageText, console.Colored[0].Text);
    }

    [Fact]
    public void RenderFrame_TextLineContainsSpinnerFromFrame()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 1);
        char expected = AiThinkingOverlay.GetSpinnerChar(1);
        Assert.Contains(expected, console.Colored[0].Text);
    }

    [Fact]
    public void RenderFrame_TextLineInnerContentIsExactlyInnerChars()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string inner = ExtractInner(console.Colored[0].Text);
        Assert.Equal(AiThinkingOverlay.Inner, inner.Length);
    }

    [Fact]
    public void RenderFrame_TextLineHasThreeSpacesBeforeSpinner()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string inner = ExtractInner(console.Colored[0].Text);
        Assert.Equal("   ", inner.Substring(0, 3));
    }

    [Fact]
    public void RenderFrame_TextLineHasTwoSpacesBetweenSpinnerAndMessage()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string inner = ExtractInner(console.Colored[0].Text);
        Assert.Equal("  ", inner.Substring(4, 2)); // positions 4-5 after "   {spinner}"
    }

    [Fact]
    public void RenderFrame_TextLineSpinnerIsAtPositionThree()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 2);
        string inner = ExtractInner(console.Colored[0].Text);
        Assert.Equal(AiThinkingOverlay.GetSpinnerChar(2), inner[3]);
    }

    // ── RenderFrame — color ───────────────────────────────────────────────────

    [Fact]
    public void RenderFrame_DefaultColor_IsGray()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(ConsoleColor.Gray, console.Colored[0].Color);
    }

    [Fact]
    public void RenderFrame_UsesSuppliedColor()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0, ConsoleColor.Blue);
        Assert.Equal(ConsoleColor.Blue, console.Colored[0].Color);
    }

    [Fact]
    public void RenderFrame_DifferentColors_ArePassedThrough()
    {
        foreach (ConsoleColor c in new[] { ConsoleColor.Red, ConsoleColor.White, ConsoleColor.DarkGray })
        {
            var console = new FakeConsole();
            AiThinkingOverlay.RenderFrame(console, 0, c);
            Assert.Equal(c, console.Colored[0].Color);
        }
    }

    // ── RenderFrame — horizontal centering ───────────────────────────────────

    [Fact]
    public void RenderFrame_TopBorderIsCenteredIn80Columns()
    {
        var console = new FakeConsole { WindowHeight = 6, WindowWidth = 80 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string top = console.Written[0].TrimEnd('\n');
        int expectedPad = (80 - (AiThinkingOverlay.Inner + 2)) / 2; // 22
        Assert.Equal(expectedPad, top.TakeWhile(c => c == ' ').Count());
    }

    // ── Show ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Show_CallsThinkAndReturnsResult()
    {
        var console = new FakeConsole();
        int result = AiThinkingOverlay.Show(console, () => 42);
        Assert.Equal(42, result);
    }

    [Fact]
    public void Show_AlwaysPositionsCursor_DoWhileGuarantee()
    {
        // do/while guarantees at least one RenderFrame call → cursor always positioned
        var console = new FakeConsole();
        AiThinkingOverlay.Show(console, () => 0);
        Assert.NotNull(console.LastCursorPosition);
    }

    [Fact]
    public void Show_UsesSuppliedColor()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.Show(console, () => 0, ConsoleColor.Red);
        Assert.Equal(ConsoleColor.Red, console.Colored[0].Color);
    }

    [Fact]
    public void Show_RendersMultipleFrames_SpinnerIncrements()
    {
        // 400ms action → do/while loop runs several times (120ms sleep between frames)
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.Show(console, () => { Thread.Sleep(400); return 0; });

        // Each frame adds one entry to Colored; verify spinner chars increment
        Assert.True(console.Colored.Count >= 2);
        char spin0 = AiThinkingOverlay.SpinnerFrames[0];
        char spin1 = AiThinkingOverlay.SpinnerFrames[1];
        Assert.Contains(spin0, console.Colored[0].Text);
        Assert.Contains(spin1, console.Colored[1].Text);
    }

    [Fact]
    public void Show_PropagatesExceptionFromThink()
    {
        var console = new FakeConsole();
        Assert.Throws<InvalidOperationException>(() =>
            AiThinkingOverlay.Show<int>(console, () => throw new InvalidOperationException("boom")));
    }
}

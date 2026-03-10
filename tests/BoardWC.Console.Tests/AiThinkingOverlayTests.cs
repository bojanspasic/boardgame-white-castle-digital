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
        public int ClearCount   { get; private set; }
        public bool Cleared => ClearCount > 0;
        public List<string> Written { get; } = new();

        public void Clear()                                   => ClearCount++;
        public void Write(string text)                        => Written.Add(text);
        public void WriteColored(string text, ConsoleColor _) => Written.Add(text + "\n");
        public void WriteLine(string text)                    => Written.Add(text + "\n");
        public ConsoleKeyInfo ReadKey(bool intercept)         => throw new NotSupportedException();
    }

    // Helper: extract the inner content (between ║ chars) from the text line
    private static string ExtractInner(string writtenLine)
    {
        string trimmed = writtenLine.TrimEnd('\n').Trim();
        int firstBar = trimmed.IndexOf('║');
        int lastBar  = trimmed.LastIndexOf('║');
        return trimmed.Substring(firstBar + 1, lastBar - firstBar - 1);
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

    // ── RenderFrame — clear & structure ──────────────────────────────────────

    [Fact]
    public void RenderFrame_ClearsScreenFirst()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.True(console.Cleared);
    }

    [Fact]
    public void RenderFrame_WritesExactlySixContentLines()
    {
        // WindowHeight=6 → topPad=0 → exactly 6 lines (top, blank, text, blankS, bottomS, shadow)
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(6, console.Written.Count);
    }

    [Fact]
    public void RenderFrame_VerticalCentering_AddsTopPadLines()
    {
        // WindowHeight=16 → topPad=(16-6)/2=5 → 5 blank lines + 6 content = 11 lines
        var console = new FakeConsole { WindowHeight = 16 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(11, console.Written.Count);
    }

    [Fact]
    public void RenderFrame_SmallWindowHeight_NoTopPad()
    {
        // WindowHeight=4 (less than 6) → topPad=0
        var console = new FakeConsole { WindowHeight = 4 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(6, console.Written.Count);
    }

    [Fact]
    public void RenderFrame_VerticalCentering_OddHeight_CorrectTopPad()
    {
        // WindowHeight=9 → topPad=(9-6)/2=1 → 1 blank + 6 content = 7 lines
        // If constant 6 mutated to 5: topPad=(9-5)/2=2 → 8 lines (kills mutation)
        var console = new FakeConsole { WindowHeight = 9 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Equal(7, console.Written.Count);
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
        Assert.Contains(console.Written, w => w.Contains('║'));
    }

    // ── RenderFrame — shadow ──────────────────────────────────────────────────

    [Fact]
    public void RenderFrame_ContainsShadowChar()
    {
        var console = new FakeConsole();
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Contains(console.Written, w => w.Contains('▒'));
    }

    [Fact]
    public void RenderFrame_ThreeBoxRowsHaveShadowOnRight()
    {
        // text row, blank-with-shadow, bottom-with-shadow all end with ▒ (before newline)
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        // textLn and blankS have ║, bottomS has ╝ — all three end with ▒
        int count = console.Written.Count(w => w.TrimEnd('\n').EndsWith('▒')
                                            && (w.Contains('║') || w.Contains('╝')));
        Assert.Equal(3, count);
    }

    [Fact]
    public void RenderFrame_ShadowRow_HasCorrectShadowCharCount()
    {
        // shadowLn = pad + " " + ▒×(Inner+2); verify exactly Inner+2 shadow chars
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string shadowRow = console.Written.Last().TrimEnd('\n');
        Assert.Equal(AiThinkingOverlay.Inner + 2, shadowRow.Count(c => c == '▒'));
    }

    [Fact]
    public void RenderFrame_ShadowRow_HasOneSpaceOffsetFromBoxLeftEdge()
    {
        // shadowLn starts at pad + " " + ▒...; the " " is 1 right of where ╔ is
        // So the char at the ╔ column is ' ' and the char at ╔+1 is '▒'
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string topRow    = console.Written[0].TrimEnd('\n');
        string shadowRow = console.Written[5].TrimEnd('\n');
        int boxLeft = topRow.IndexOf('╔');
        Assert.Equal(' ',  shadowRow[boxLeft]);      // one-space offset at box-left col
        Assert.Equal('▒', shadowRow[boxLeft + 1]);  // shadow begins one col to the right
    }

    // ── RenderFrame — top border exact structure ──────────────────────────────

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
    public void RenderFrame_BottomBorderStartsWithCornerAndEndsWithShadow()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string bottom = console.Written[4].TrimEnd('\n').Trim();
        Assert.StartsWith("╚", bottom);
        Assert.EndsWith("▒", bottom);
    }

    // ── RenderFrame — text line content & spacing ─────────────────────────────

    [Fact]
    public void RenderFrame_TextLineContainsMessageText()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        Assert.Contains(console.Written, w => w.Contains(AiThinkingOverlay.MessageText));
    }

    [Fact]
    public void RenderFrame_TextLineContainsSpinnerFromFrame()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 1);
        char expected = AiThinkingOverlay.GetSpinnerChar(1);
        string textLine = console.Written.First(w => w.Contains(AiThinkingOverlay.MessageText));
        Assert.Contains(expected, textLine);
    }

    [Fact]
    public void RenderFrame_TextLineInnerContentIsExactlyInnerChars()
    {
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string textLine = console.Written.First(w => w.Contains(AiThinkingOverlay.MessageText));
        string inner = ExtractInner(textLine);
        Assert.Equal(AiThinkingOverlay.Inner, inner.Length);
    }

    [Fact]
    public void RenderFrame_TextLineHasThreeSpacesBeforeSpinner()
    {
        // inner layout: "   {spinner}  {message...}"
        // TextPrefix = 3 spaces before spinner
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string textLine = console.Written.First(w => w.Contains(AiThinkingOverlay.MessageText));
        string inner = ExtractInner(textLine);
        Assert.Equal("   ", inner.Substring(0, 3)); // exactly 3 leading spaces
    }

    [Fact]
    public void RenderFrame_TextLineHasTwoSpacesBetweenSpinnerAndMessage()
    {
        // TextGap = 2 spaces between spinner char and MessageText
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string textLine = console.Written.First(w => w.Contains(AiThinkingOverlay.MessageText));
        string inner = ExtractInner(textLine);
        // Position 3 is spinner, positions 4-5 are the two gap spaces
        Assert.Equal("  ", inner.Substring(4, 2));
    }

    [Fact]
    public void RenderFrame_TextLineSpinnerIsAtPositionThree()
    {
        // Inner[3] should be the spinner char for the given frame
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 2);
        char expectedSpinner = AiThinkingOverlay.GetSpinnerChar(2);
        string textLine = console.Written.First(w => w.Contains(AiThinkingOverlay.MessageText));
        string inner = ExtractInner(textLine);
        Assert.Equal(expectedSpinner, inner[3]);
    }

    // ── RenderFrame — blank rows and bottom border inner widths ──────────────

    [Fact]
    public void RenderFrame_FirstBlankRow_HasExactlyInnerSpacesBetweenBorders()
    {
        // blank = pad + ║ + " "×Inner + ║  — inner space count must equal Inner
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string blankRow = console.Written[1].TrimEnd('\n').Trim(); // second line (index 1)
        int firstBar = blankRow.IndexOf('║');
        int lastBar  = blankRow.LastIndexOf('║');
        string spaces = blankRow.Substring(firstBar + 1, lastBar - firstBar - 1);
        Assert.Equal(AiThinkingOverlay.Inner, spaces.Length);
    }

    [Fact]
    public void RenderFrame_ShadowBlankRow_HasExactlyInnerSpacesBetweenBorders()
    {
        // blankS = pad + ║ + " "×Inner + ║▒  — inner space count must equal Inner
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        // fourth line (index 3) is blankS — ends with ║▒
        string blankS = console.Written[3].TrimEnd('\n').Trim(); // "║  ...  ║▒"
        int firstBar = blankS.IndexOf('║');
        int lastBar  = blankS.LastIndexOf('║');
        string spaces = blankS.Substring(firstBar + 1, lastBar - firstBar - 1);
        Assert.Equal(AiThinkingOverlay.Inner, spaces.Length);
    }

    [Fact]
    public void RenderFrame_BottomBorderHasCorrectHorizontalCount()
    {
        // bottomS = pad + ╚ + ═×Inner + ╝▒  — ═ count must equal Inner
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string bottom = console.Written[4].TrimEnd('\n').Trim();
        Assert.Equal(AiThinkingOverlay.Inner, bottom.Count(c => c == '═'));
    }

    // ── RenderFrame — horizontal centering ───────────────────────────────────

    [Fact]
    public void RenderFrame_TopBorderIsCenteredIn80Columns()
    {
        var console = new FakeConsole { WindowHeight = 6, WindowWidth = 80 };
        AiThinkingOverlay.RenderFrame(console, 0);
        string top = console.Written[0].TrimEnd('\n');
        int boxWidth    = AiThinkingOverlay.Inner + 2; // 36
        int expectedPad = (80 - boxWidth) / 2;         // 22
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
    public void Show_AlwaysRendersAtLeastOneFrame()
    {
        // do/while guarantees at least one RenderFrame call (Clear is called inside it)
        var console = new FakeConsole();
        AiThinkingOverlay.Show(console, () => 0);
        Assert.True(console.ClearCount >= 1);
    }

    [Fact]
    public void Show_RendersMultipleFrames_SpinnerIncrements()
    {
        // Use a 400ms action so the do/while loop runs several times (120ms sleep between frames)
        // Verifies frame++ increments: second text line must use SpinnerFrames[1], not [0]
        var console = new FakeConsole { WindowHeight = 6 };
        AiThinkingOverlay.Show(console, () => { Thread.Sleep(400); return 0; });

        var textLines = console.Written
            .Where(w => w.Contains(AiThinkingOverlay.MessageText))
            .Select(w => ExtractInner(w))
            .ToList();

        Assert.True(textLines.Count >= 2);
        // First render: spinner at SpinnerFrames[0]; second: SpinnerFrames[1]
        Assert.Equal(AiThinkingOverlay.SpinnerFrames[0], textLines[0][3]);
        Assert.Equal(AiThinkingOverlay.SpinnerFrames[1], textLines[1][3]);
    }

    [Fact]
    public void Show_PropagatesExceptionFromThink()
    {
        var console = new FakeConsole();
        Assert.Throws<InvalidOperationException>(() =>
            AiThinkingOverlay.Show<int>(console, () => throw new InvalidOperationException("boom")));
    }
}

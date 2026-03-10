using BoardWC.Console.UI;
using static BoardWC.Console.UI.MainMenu;

namespace BoardWC.Console.Tests;

/// <summary>
/// Unit tests for MainMenu — navigation, player selection, overlay, rendering, and helpers.
/// </summary>
public class MainMenuTests
{
    // ── Fake console ──────────────────────────────────────────────────────────

    private sealed class FakeConsole : IConsoleIO
    {
        private readonly Queue<ConsoleKey> _keys;

        public FakeConsole(params ConsoleKey[] keys) =>
            _keys = new Queue<ConsoleKey>(keys);

        public int WindowWidth { get; set; } = 80;
        public bool Cleared { get; private set; }
        public List<string> Written { get; } = new();
        public List<(string Text, ConsoleColor Color)> Colored { get; } = new();
        public bool? LastReadKeyIntercept { get; private set; }

        public void Clear() => Cleared = true;
        public void Write(string text) => Written.Add(text);
        public void WriteColored(string text, ConsoleColor color) => Colored.Add((text, color));
        public void WriteLine(string text) => Written.Add(text + "\n");

        public ConsoleKeyInfo ReadKey(bool intercept)
        {
            LastReadKeyIntercept = intercept;
            var key = _keys.Dequeue();
            return new ConsoleKeyInfo('\0', key, false, false, false);
        }
    }

    // ── Advance helper ────────────────────────────────────────────────────────

    [Fact]
    public void Advance_Empty_ReturnsHuman()
    {
        Assert.Equal(PlayerType.Human, Advance(PlayerType.Empty));
    }

    [Fact]
    public void Advance_Human_ReturnsAi()
    {
        Assert.Equal(PlayerType.Ai, Advance(PlayerType.Human));
    }

    [Fact]
    public void Advance_Ai_ReturnsEmpty()
    {
        Assert.Equal(PlayerType.Empty, Advance(PlayerType.Ai));
    }

    // ── SelectedCount helper ──────────────────────────────────────────────────

    [Fact]
    public void SelectedCount_AllEmpty_ReturnsZero()
    {
        Assert.Equal(0, SelectedCount([PlayerType.Empty, PlayerType.Empty, PlayerType.Empty, PlayerType.Empty]));
    }

    [Fact]
    public void SelectedCount_OneHuman_ReturnsOne()
    {
        Assert.Equal(1, SelectedCount([PlayerType.Human, PlayerType.Empty, PlayerType.Empty, PlayerType.Empty]));
    }

    [Fact]
    public void SelectedCount_TwoSelected_ReturnsTwo()
    {
        Assert.Equal(2, SelectedCount([PlayerType.Human, PlayerType.Ai, PlayerType.Empty, PlayerType.Empty]));
    }

    [Fact]
    public void SelectedCount_AllSelected_ReturnsFour()
    {
        Assert.Equal(4, SelectedCount([PlayerType.Human, PlayerType.Ai, PlayerType.Human, PlayerType.Ai]));
    }

    // ── Center helper ─────────────────────────────────────────────────────────

    [Fact]
    public void Center_NarrowWidth_ReturnsTextDirectly()
    {
        Assert.Equal("HELLO", Center("HELLO", 3));
    }

    [Fact]
    public void Center_ExactWidth_ReturnsTextDirectly()
    {
        Assert.Equal("HELLO", Center("HELLO", 5));
    }

    [Fact]
    public void Center_WideWidth_PadsCorrectly()
    {
        // "HELLO" (5 chars), width=15 → padding=(15-5)/2=5
        Assert.Equal("     HELLO", Center("HELLO", 15));
    }

    [Fact]
    public void Center_OddRemainder_UsesIntegerDivision()
    {
        // "HELLO" (5 chars), width=16 → padding=(16-5)/2=5
        Assert.Equal("     HELLO", Center("HELLO", 16));
    }

    // ── Show — basic flow ─────────────────────────────────────────────────────

    [Fact]
    public void Show_TwoHumans_ReturnsCorrectTypes()
    {
        // Space (P1=H), Down, Space (P2=H), Enter → 2 selected → return
        var console = new FakeConsole(
            ConsoleKey.Spacebar, ConsoleKey.DownArrow,
            ConsoleKey.Spacebar, ConsoleKey.Enter);

        var result = Show(console);

        Assert.Equal(PlayerType.Human, result[0]);
        Assert.Equal(PlayerType.Human, result[1]);
        Assert.Equal(PlayerType.Empty, result[2]);
        Assert.Equal(PlayerType.Empty, result[3]);
    }

    [Fact]
    public void Show_HumanAndAi_ReturnsCorrectTypes()
    {
        // Space (P1=H), Down, Space (P2=H), Space (P2=A), Enter → H+A
        var console = new FakeConsole(
            ConsoleKey.Spacebar, ConsoleKey.DownArrow,
            ConsoleKey.Spacebar, ConsoleKey.Spacebar, ConsoleKey.Enter);

        var result = Show(console);

        Assert.Equal(PlayerType.Human, result[0]);
        Assert.Equal(PlayerType.Ai, result[1]);
    }

    [Fact]
    public void Show_FourPlayers_ReturnsAll()
    {
        var console = new FakeConsole(
            ConsoleKey.Spacebar,                       // P1 = H
            ConsoleKey.DownArrow, ConsoleKey.Spacebar, // P2 = H
            ConsoleKey.DownArrow, ConsoleKey.Spacebar, // P3 = H
            ConsoleKey.DownArrow, ConsoleKey.Spacebar, // P4 = H
            ConsoleKey.Enter);

        var result = Show(console);

        Assert.All(result, t => Assert.Equal(PlayerType.Human, t));
    }

    // ── Show — navigation ─────────────────────────────────────────────────────

    [Fact]
    public void Show_DownArrow_MovesToNextPlayer()
    {
        // Down to P2, Space (P2=H), Down to P3, Space (P3=H), Enter
        var console = new FakeConsole(
            ConsoleKey.DownArrow, ConsoleKey.Spacebar,
            ConsoleKey.DownArrow, ConsoleKey.Spacebar,
            ConsoleKey.Enter);

        var result = Show(console);

        Assert.Equal(PlayerType.Empty, result[0]); // P1 never touched
        Assert.Equal(PlayerType.Human, result[1]);
        Assert.Equal(PlayerType.Human, result[2]);
    }

    [Fact]
    public void Show_DownArrow_WrapsBeyondLastPlayer()
    {
        // 4× Down brings cursor back to 0; Space sets P1=H; Down sets P2=H; Enter
        var console = new FakeConsole(
            ConsoleKey.DownArrow, ConsoleKey.DownArrow,
            ConsoleKey.DownArrow, ConsoleKey.DownArrow, // back at 0
            ConsoleKey.Spacebar,                        // P1 = H
            ConsoleKey.DownArrow, ConsoleKey.Spacebar,  // P2 = H
            ConsoleKey.Enter);

        var result = Show(console);

        Assert.Equal(PlayerType.Human, result[0]);
        Assert.Equal(PlayerType.Human, result[1]);
    }

    [Fact]
    public void Show_UpArrow_FromFirst_WrapsToLast()
    {
        // Up from P1 → cursor=3; Space (P4=H); Down→P1; Space (P1=H); Enter
        var console = new FakeConsole(
            ConsoleKey.UpArrow,   ConsoleKey.Spacebar,  // P4 = H
            ConsoleKey.DownArrow, ConsoleKey.Spacebar,  // P1 = H (cursor 3→0)
            ConsoleKey.Enter);

        var result = Show(console);

        Assert.Equal(PlayerType.Human, result[0]);
        Assert.Equal(PlayerType.Human, result[3]);
    }

    [Fact]
    public void Show_UpArrow_MovesToPreviousPlayer()
    {
        // Down→P2; Up→back to P1; Space (P1=H); Down; Space (P2=H); Enter
        var console = new FakeConsole(
            ConsoleKey.DownArrow,
            ConsoleKey.UpArrow,
            ConsoleKey.Spacebar,                        // P1 = H
            ConsoleKey.DownArrow, ConsoleKey.Spacebar,  // P2 = H
            ConsoleKey.Enter);

        var result = Show(console);

        Assert.Equal(PlayerType.Human, result[0]);
        Assert.Equal(PlayerType.Human, result[1]);
    }

    // ── Show — space cycling ──────────────────────────────────────────────────

    [Fact]
    public void Show_ThreeSpaces_CyclesBackToEmpty()
    {
        // P1: Empty→H→A→Empty; then set P2=H; Enter
        var console = new FakeConsole(
            ConsoleKey.Spacebar, ConsoleKey.Spacebar, ConsoleKey.Spacebar, // P1 cycles to Empty
            ConsoleKey.DownArrow, ConsoleKey.Spacebar,                     // P2 = H
            ConsoleKey.DownArrow, ConsoleKey.Spacebar,                     // P3 = H
            ConsoleKey.Enter);

        var result = Show(console);

        Assert.Equal(PlayerType.Empty, result[0]); // cycled back to empty
        Assert.Equal(PlayerType.Human, result[1]);
        Assert.Equal(PlayerType.Human, result[2]);
    }

    // ── Show — overlay (validation) ───────────────────────────────────────────

    [Fact]
    public void Show_EnterWithZeroPlayers_ShowsOverlayThenContinues()
    {
        // Enter (0 players) → overlay; Enter (dismiss); Space (P1=H); Down; Space (P2=H); Enter
        var console = new FakeConsole(
            ConsoleKey.Enter,                           // shows overlay
            ConsoleKey.Enter,                           // dismisses overlay
            ConsoleKey.Spacebar,                        // P1 = H
            ConsoleKey.DownArrow, ConsoleKey.Spacebar,  // P2 = H
            ConsoleKey.Enter);

        var result = Show(console);

        Assert.Equal(PlayerType.Human, result[0]);
        Assert.Equal(PlayerType.Human, result[1]);
    }

    [Fact]
    public void Show_EnterWithOnePlayer_ShowsOverlay()
    {
        // Space (P1=H), Enter (1 player → overlay), Enter (dismiss), Down, Space (P2=H), Enter
        var console = new FakeConsole(
            ConsoleKey.Spacebar,
            ConsoleKey.Enter,                           // 1 player → overlay
            ConsoleKey.Enter,                           // dismiss
            ConsoleKey.DownArrow, ConsoleKey.Spacebar,  // P2 = H
            ConsoleKey.Enter);

        var result = Show(console);

        Assert.Equal(PlayerType.Human, result[0]);
        Assert.Equal(PlayerType.Human, result[1]);
    }

    [Fact]
    public void Show_OverlayIgnoresNonEnterKeys()
    {
        // Overlay active; Down/Space/Up are ignored; Enter dismisses
        var console = new FakeConsole(
            ConsoleKey.Enter,                                              // overlay
            ConsoleKey.DownArrow, ConsoleKey.Spacebar, ConsoleKey.UpArrow, // ignored while overlay
            ConsoleKey.Enter,                                              // dismiss overlay
            ConsoleKey.Spacebar,                                           // P1 = H
            ConsoleKey.DownArrow, ConsoleKey.Spacebar,                     // P2 = H
            ConsoleKey.Enter);

        var result = Show(console);

        // P1=H (cursor stayed at 0 during overlay)
        Assert.Equal(PlayerType.Human, result[0]);
        Assert.Equal(PlayerType.Human, result[1]);
    }

    // ── Show — ReadKey intercept ───────────────────────────────────────────────

    [Fact]
    public void Show_ReadKey_CalledWithIntercept_True()
    {
        var console = new FakeConsole(
            ConsoleKey.Spacebar, ConsoleKey.DownArrow,
            ConsoleKey.Spacebar, ConsoleKey.Enter);
        Show(console);
        Assert.True(console.LastReadKeyIntercept);
    }

    // ── Render — structure ────────────────────────────────────────────────────

    [Fact]
    public void Render_ClearsScreen()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.True(console.Cleared);
    }

    [Fact]
    public void Render_WritesSelectPlayersTitle()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Contains(console.Written, w => w.Contains(SelectTitle));
    }

    [Fact]
    public void Render_WritesHintText()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Contains(console.Written, w => w.Contains(HintText));
    }

    [Fact]
    public void Render_WritesFourPlayerRows()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Equal(4, console.Colored.Count);
    }

    [Fact]
    public void Render_Player1Row_ShowsMarkerWhenCursor0()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Contains("<", console.Colored[0].Text);
    }

    [Fact]
    public void Render_Player2Row_NoMarkerWhenCursor0()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.DoesNotContain("<", console.Colored[1].Text);
    }

    [Fact]
    public void Render_Player1Row_ShowsHWhenHuman()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        var types = new[] { PlayerType.Human, PlayerType.Empty, PlayerType.Empty, PlayerType.Empty };
        Render(console, types, 0, false);
        Assert.Contains("[H]", console.Colored[0].Text);
    }

    [Fact]
    public void Render_Player1Row_ShowsAWhenAi()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        var types = new[] { PlayerType.Ai, PlayerType.Empty, PlayerType.Empty, PlayerType.Empty };
        Render(console, types, 0, false);
        Assert.Contains("[A]", console.Colored[0].Text);
    }

    [Fact]
    public void Render_Player1Row_ShowsSpaceWhenEmpty()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Contains("[ ]", console.Colored[0].Text);
    }

    [Fact]
    public void Render_Player1IsBlue()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Equal(ConsoleColor.Blue, console.Colored[0].Color);
    }

    [Fact]
    public void Render_Player2IsRed()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Equal(ConsoleColor.Red, console.Colored[1].Color);
    }

    [Fact]
    public void Render_Player3IsGreen()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Equal(ConsoleColor.Green, console.Colored[2].Color);
    }

    [Fact]
    public void Render_Player4IsYellow()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Equal(ConsoleColor.Yellow, console.Colored[3].Color);
    }

    [Fact]
    public void Render_SelectTitleIsCentered()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);

        int expected = (80 - SelectTitle.Length) / 2;
        var line = console.Written.First(w => w.Contains(SelectTitle));
        Assert.Equal(new string(' ', expected) + SelectTitle, line.TrimEnd('\n'));
    }

    [Fact]
    public void Render_NoOverlay_WhenFalse()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.DoesNotContain(console.Written, w => w.Contains(OverlayLine1));
    }

    [Fact]
    public void Render_ShowsOverlay_WhenTrue()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, true);
        Assert.Contains(console.Written, w => w.Contains(OverlayLine1));
        Assert.Contains(console.Written, w => w.Contains(OverlayLine2));
    }

    // ── RenderOverlay ─────────────────────────────────────────────────────────

    [Fact]
    public void RenderOverlay_ContainsLine1()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        RenderOverlay(console, 80);
        Assert.Contains(console.Written, w => w.Contains(OverlayLine1));
    }

    [Fact]
    public void RenderOverlay_ContainsLine2()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        RenderOverlay(console, 80);
        Assert.Contains(console.Written, w => w.Contains(OverlayLine2));
    }

    [Fact]
    public void RenderOverlay_ContainsBorderChars()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        RenderOverlay(console, 80);
        Assert.Contains(console.Written, w => w.Contains('╔') || w.Contains('╚'));
        Assert.Contains(console.Written, w => w.Contains("║"));
    }

    [Fact]
    public void RenderOverlay_LinesAreCentered()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        RenderOverlay(console, 80);
        // Top border should be centered: leading spaces before the box corner
        var topLine = console.Written.First(w => w.Contains("╔") && w.Contains("═"));
        Assert.StartsWith(" ", topLine);
    }

    // ── RenderOverlay — exact structure ───────────────────────────────────────

    [Fact]
    public void RenderOverlay_BorderDashCountMatchesLongerLine()
    {
        // inner = Math.Max(Line1.Length, Line2.Length) → horizontal chars = inner + 2
        var console = new FakeConsole() { WindowWidth = 200 };
        RenderOverlay(console, 200);
        var topLine = console.Written.First(w => w.Contains("╔") && w.Contains("═"))
                                     .TrimEnd('\n').Trim();
        int expectedHorizontal = OverlayLine1.Length + 2; // Line1 is the longer one
        Assert.Equal(expectedHorizontal, topLine.Count(c => c == '═'));
    }

    [Fact]
    public void RenderOverlay_HasTopAndBottomBorders()
    {
        // Top (╔...╗) and bottom (╚...╝) are distinct lines, each appearing once
        var console = new FakeConsole() { WindowWidth = 200 };
        RenderOverlay(console, 200);
        string expectedTop    = "╔" + new string('═', OverlayLine1.Length + 2) + "╗";
        string expectedBottom = "╚" + new string('═', OverlayLine1.Length + 2) + "╝";
        Assert.Equal(1, console.Written.Count(w => w.TrimEnd('\n').Trim() == expectedTop));
        Assert.Equal(1, console.Written.Count(w => w.TrimEnd('\n').Trim() == expectedBottom));
    }

    [Fact]
    public void RenderOverlay_Mid1StartsAndEndsWithBorder()
    {
        var console = new FakeConsole() { WindowWidth = 200 };
        RenderOverlay(console, 200);
        var mid1 = console.Written.First(w => w.Contains(OverlayLine1)).TrimEnd('\n').Trim();
        Assert.StartsWith("║ ", mid1);
        Assert.EndsWith(" ║", mid1);
    }

    [Fact]
    public void RenderOverlay_Mid2StartsAndEndsWithBorder()
    {
        var console = new FakeConsole() { WindowWidth = 200 };
        RenderOverlay(console, 200);
        var mid2 = console.Written
            .Where(w => w.Contains(OverlayLine2))
            .Select(w => w.TrimEnd('\n').Trim())
            .First(w => !w.Contains(OverlayLine1));
        Assert.StartsWith("║ ", mid2);
        Assert.EndsWith(" ║", mid2);
    }

    // ── Render — player row text ───────────────────────────────────────────────

    [Fact]
    public void Render_PlayerRowsContainCorrectPlayerNumbers()
    {
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Contains("PLAYER 1", console.Colored[0].Text);
        Assert.Contains("PLAYER 2", console.Colored[1].Text);
        Assert.Contains("PLAYER 3", console.Colored[2].Text);
        Assert.Contains("PLAYER 4", console.Colored[3].Text);
    }

    [Fact]
    public void Render_WritesTitleText()
    {
        // Verifies that SplashScreen.TitleText (block chars) is written in Render
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Contains(console.Written, w => w.Contains("█"));
    }

    [Fact]
    public void Render_NonCursorRow_HasTwoTrailingSpaces()
    {
        // Arrow for non-cursor row is "  " (two spaces), not "" or anything else
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false); // cursor=0
        // Player 2 (index 1) is not the cursor
        string rowText = console.Colored[1].Text.TrimStart(); // strip centering padding
        Assert.EndsWith("[ ]  ", rowText);
    }

    [Fact]
    public void Render_CursorRow_HasArrowSuffix()
    {
        // Arrow for cursor row is " <"
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 2, false); // cursor=2
        string rowText = console.Colored[2].Text.TrimStart();
        Assert.EndsWith("[ ] <", rowText);
    }

    [Fact]
    public void Render_HasExactlyTwoBlankLines()
    {
        // Render writes "" twice: once after title, once before hint
        var console = new FakeConsole() { WindowWidth = 80 };
        Render(console, new PlayerType[4], 0, false);
        Assert.Equal(2, console.Written.Count(w => w == "\n"));
    }

    // ── Show — Render is called ────────────────────────────────────────────────

    [Fact]
    public void Show_ClearsScreenDuringLoop()
    {
        // Show calls Render on every iteration; Render calls Clear
        var console = new FakeConsole(
            ConsoleKey.Spacebar, ConsoleKey.DownArrow,
            ConsoleKey.Spacebar, ConsoleKey.Enter);
        Show(console);
        Assert.True(console.Cleared);
    }

    // ── RenderOverlay — mid2 line format ──────────────────────────────────────

    [Fact]
    public void RenderOverlay_Mid2ContainsLine2TextBetweenBorders()
    {
        // The mid2 line is "| {OverlayLine2.PadRight(inner)} |" — verify entire structure
        var console = new FakeConsole() { WindowWidth = 200 };
        RenderOverlay(console, 200);
        // Find raw written line for mid2 (contains OverlayLine2, not OverlayLine1)
        var rawLine = console.Written.First(w => w.Contains(OverlayLine2) && !w.Contains(OverlayLine1));
        // Strip centering and newline to get the box content
        string content = rawLine.TrimEnd('\n').Trim();
        Assert.StartsWith("║ ", content);
        // The line must end with the PadRight padding followed by " ║"
        // OverlayLine1 is longer so inner = OverlayLine1.Length; OverlayLine2 is padded
        int expectedContentLength = 2 + OverlayLine1.Length + 2; // "║ " + inner + " ║"
        Assert.Equal(expectedContentLength, content.Length);
    }
}

using BoardWC.Console.UI;

namespace BoardWC.Console.Tests;

/// <summary>
/// Unit tests for SplashScreen — display and key-handling behaviour.
/// </summary>
public class SplashScreenTests
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
        public bool? LastReadKeyIntercept { get; private set; }

        public void Clear() => Cleared = true;
        public void Write(string text) => Written.Add(text);
        public void WriteLine(string text) => Written.Add(text + "\n");

        public ConsoleKeyInfo ReadKey(bool intercept)
        {
            LastReadKeyIntercept = intercept;
            var key = _keys.Dequeue();
            return new ConsoleKeyInfo('\0', key, false, false, false);
        }
    }

    // ── TitleText static content ──────────────────────────────────────────────

    [Fact]
    public void TitleText_IsNotEmpty()
    {
        Assert.NotEmpty(SplashScreen.TitleText);
    }

    [Fact]
    public void TitleText_ContainsBlockChars()
    {
        Assert.Contains("█", SplashScreen.TitleText);
    }

    // ── Show — screen setup ───────────────────────────────────────────────────

    [Fact]
    public void Show_ClearsScreenFirst()
    {
        var console = new FakeConsole(ConsoleKey.Spacebar);
        SplashScreen.Show(console);
        Assert.True(console.Cleared);
    }

    [Fact]
    public void Show_WritesTitleText()
    {
        var console = new FakeConsole(ConsoleKey.Spacebar);
        SplashScreen.Show(console);
        Assert.Contains(console.Written, w => w.Contains("█"));
    }

    // ── Show — prompt ─────────────────────────────────────────────────────────

    [Fact]
    public void Show_WritesPromptText()
    {
        var console = new FakeConsole(ConsoleKey.Spacebar);
        SplashScreen.Show(console);
        Assert.Contains(console.Written, w => w.Contains(SplashScreen.Prompt));
    }

    [Fact]
    public void Show_PromptIsCentered_WidthLargerThanPrompt()
    {
        // WindowWidth = 80, prompt.Length = 28 → padding = (80 - 28) / 2 = 26
        var console = new FakeConsole(ConsoleKey.Spacebar) { WindowWidth = 80 };
        SplashScreen.Show(console);

        int expectedPadding = (80 - SplashScreen.Prompt.Length) / 2;
        var promptLine = console.Written.First(w => w.Contains(SplashScreen.Prompt));
        // Exactly expectedPadding spaces, then the prompt starts
        Assert.Equal(new string(' ', expectedPadding) + SplashScreen.Prompt, promptLine.TrimEnd('\n').TrimEnd());
    }

    [Fact]
    public void Show_PromptIsCentered_OddRemainder()
    {
        // WindowWidth = 81 → (81 - 28) / 2 = 26 (integer division, same as 80)
        var console = new FakeConsole(ConsoleKey.Spacebar) { WindowWidth = 81 };
        SplashScreen.Show(console);

        int expectedPadding = (81 - SplashScreen.Prompt.Length) / 2;
        var promptLine = console.Written.First(w => w.Contains(SplashScreen.Prompt));
        Assert.Equal(new string(' ', expectedPadding) + SplashScreen.Prompt, promptLine.TrimEnd('\n').TrimEnd());
    }

    [Fact]
    public void Show_PromptCentering_NarrowWindow_NoPaddingBelowZero()
    {
        // WindowWidth smaller than prompt → padding clamped to 0
        var console = new FakeConsole(ConsoleKey.Spacebar) { WindowWidth = 10 };
        SplashScreen.Show(console);

        var promptLine = console.Written.First(w => w.Contains(SplashScreen.Prompt));
        // Starts directly with the prompt (no leading spaces)
        Assert.StartsWith(SplashScreen.Prompt, promptLine);
    }

    [Fact]
    public void Show_PromptCentering_ExactWidth_ZeroPadding()
    {
        // WindowWidth == prompt length → padding = 0
        var console = new FakeConsole(ConsoleKey.Spacebar) { WindowWidth = SplashScreen.Prompt.Length };
        SplashScreen.Show(console);

        var promptLine = console.Written.First(w => w.Contains(SplashScreen.Prompt));
        Assert.StartsWith(SplashScreen.Prompt, promptLine);
    }

    // ── Show — key handling ───────────────────────────────────────────────────

    [Fact]
    public void Show_SpaceKey_Proceeds()
    {
        // Should complete without hanging
        var console = new FakeConsole(ConsoleKey.Spacebar);
        SplashScreen.Show(console);
    }

    [Fact]
    public void Show_OtherKeysIgnored_SpaceEventuallyProceeds()
    {
        // Enter and Escape are ignored; only Spacebar exits the loop
        var console = new FakeConsole(ConsoleKey.Enter, ConsoleKey.Escape, ConsoleKey.A, ConsoleKey.Spacebar);
        SplashScreen.Show(console);
    }

    [Fact]
    public void Show_SingleNonSpaceKey_ThenSpace_Proceeds()
    {
        var console = new FakeConsole(ConsoleKey.Enter, ConsoleKey.Spacebar);
        SplashScreen.Show(console);
    }

    [Fact]
    public void Show_ReadKey_CalledWithIntercept_True()
    {
        // intercept=true suppresses echo; must not be mutated to false
        var console = new FakeConsole(ConsoleKey.Spacebar);
        SplashScreen.Show(console);
        Assert.True(console.LastReadKeyIntercept);
    }
}

namespace BoardWC.Console.UI;

internal static class AiThinkingOverlay
{
    internal const string MessageText = "AI player is thinking";

    // Spinner: Braille dot-pattern chars that create a rotating animation
    internal static readonly char[] SpinnerFrames = ['\u280B', '\u2819', '\u2839', '\u2838', '\u283C', '\u2834', '\u2826', '\u2827', '\u2807', '\u280F'];

    // Inner content width (═ count between ╔ and ╗)
    // Layout: TextPrefix(3) + spinner(1) + TextGap(2) + MessageText(21) + RightPad(7) = 34
    private const int TextPrefix = 3;
    private const int TextGap    = 2;
    private const int RightPad   = 7;
    internal const int Inner = TextPrefix + 1 + TextGap + 21 /* MessageText.Length */ + RightPad; // 34

    private const int BoxLines = 6; // top + blank + textLn + blankS + bottomS + shadowLine

    internal static char GetSpinnerChar(int frame) =>
        SpinnerFrames[Math.Abs(frame) % SpinnerFrames.Length];

    internal static void RenderFrame(IConsoleIO console, int frame,
                                      ConsoleColor color = ConsoleColor.Gray)
    {
        string p = new string(' ', ConsoleBox.HorizPad(Inner));

        // Text line inner: 3 spaces + spinner + 2 spaces + message padded to fill Inner
        string textContent = new string(' ', TextPrefix)
                           + GetSpinnerChar(frame)
                           + new string(' ', TextGap)
                           + MessageText.PadRight(Inner - TextPrefix - 1 - TextGap); // PadRight(28)

        // Vertical centering via cursor position — leaves existing screen content intact
        int topPad = ConsoleBox.VertPad(console.WindowHeight, BoxLines);
        console.SetCursorPosition(0, topPad);
        console.WriteLine(ConsoleBox.TopBorder(p, Inner));
        console.WriteLine(ConsoleBox.BlankRow(p, Inner));
        console.WriteColored(ConsoleBox.ContentRowShadow(p, Inner, textContent), color);
        console.WriteLine(ConsoleBox.BlankRowShadow(p, Inner));
        console.WriteLine(ConsoleBox.BottomBorder(p, Inner));
        console.WriteLine(ConsoleBox.ShadowLine(p, Inner));
    }

    internal static T Show<T>(IConsoleIO console, Func<T> think,
                               ConsoleColor color = ConsoleColor.Gray)
    {
        int frame = 0;
        Task<T> task = Task.Run(think);
        do
        {
            RenderFrame(console, frame++, color);
            Thread.Sleep(120);
        }
        while (!task.IsCompleted);
        return task.GetAwaiter().GetResult();
    }
}

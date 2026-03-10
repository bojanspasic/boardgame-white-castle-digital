namespace BoardWC.Console.UI;

internal static class AiThinkingOverlay
{
    internal const string MessageText = "AI player is thinking";

    // Spinner: Unicode arc chars that create a rotating-corner illusion
    internal static readonly char[] SpinnerFrames = ['\u256D', '\u256E', '\u256F', '\u2570'];

    private const int DisplayWidth = 80;

    // Inner content width (═ count between ╔ and ╗)
    // Layout: TextPrefix(3) + spinner(1) + TextGap(2) + MessageText(21) + RightPad(7) = 34
    private const int TextPrefix = 3;
    private const int TextGap    = 2;
    private const int RightPad   = 7;
    internal const int Inner = TextPrefix + 1 + TextGap + 21 /* MessageText.Length */ + RightPad; // 34

    internal static char GetSpinnerChar(int frame) =>
        SpinnerFrames[Math.Abs(frame) % SpinnerFrames.Length];

    internal static void RenderFrame(IConsoleIO console, int frame)
    {
        int boxWidth = Inner + 2; // 36 — width of ╔...╗
        int leftPad  = Math.Max(0, (DisplayWidth - boxWidth) / 2); // 22
        string pad   = new string(' ', leftPad);

        // Text line inner: 3 spaces + spinner + 2 spaces + message padded to fill Inner
        string textContent = new string(' ', TextPrefix)
                           + GetSpinnerChar(frame)
                           + new string(' ', TextGap)
                           + MessageText.PadRight(Inner - TextPrefix - 1 - TextGap); // PadRight(28)

        string top     = pad + "\u2554" + new string('\u2550', Inner) + "\u2557";
        string blank   = pad + "\u2551" + new string(' ',      Inner) + "\u2551";
        string textLn  = pad + "\u2551" + textContent               + "\u2551\u2592";
        string blankS  = pad + "\u2551" + new string(' ',      Inner) + "\u2551\u2592";
        string bottomS = pad + "\u255A" + new string('\u2550',  Inner) + "\u255D\u2592";
        string shadowLn= pad + " "      + new string('\u2592',  Inner + 2); // 1 space + 36 ▒

        // Vertical centering: 5 box rows + 1 shadow row = 6 display rows
        int topPad = Math.Max(0, (console.WindowHeight - 6) / 2);

        console.Clear();
        for (int i = 0; i < topPad; i++)
            console.WriteLine("");
        console.WriteLine(top);
        console.WriteLine(blank);
        console.WriteLine(textLn);
        console.WriteLine(blankS);
        console.WriteLine(bottomS);
        console.WriteLine(shadowLn);
    }

    internal static T Show<T>(IConsoleIO console, Func<T> think)
    {
        int frame = 0;
        Task<T> task = Task.Run(think);
        do
        {
            RenderFrame(console, frame++);
            Thread.Sleep(120);
        }
        while (!task.IsCompleted);
        return task.GetAwaiter().GetResult();
    }
}

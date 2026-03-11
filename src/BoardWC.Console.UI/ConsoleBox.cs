namespace BoardWC.Console.UI;

/// <summary>
/// Shared helpers for drawing Unicode double-line boxes with drop shadow,
/// horizontally and vertically centered in an 80-column terminal.
/// </summary>
internal static class ConsoleBox
{
    internal const int DisplayWidth = 80;

    // Number of spaces to left-indent so a box of total width (inner+2) is centered
    internal static int HorizPad(int inner) =>
        Math.Max(0, (DisplayWidth - inner - 2) / 2);

    // Number of lines to skip so a box of 'boxLines' rows is vertically centered
    internal static int VertPad(int windowHeight, int boxLines) =>
        Math.Max(0, (windowHeight - boxLines) / 2);

    // ── Row builders ──────────────────────────────────────────────────────────

    internal static string TopBorder(string p, int n) =>
        p + "╔" + new string('═', n) + "╗";

    internal static string BlankRow(string p, int n) =>
        p + "║" + new string(' ', n) + "║";

    internal static string BlankRowShadow(string p, int n) =>
        p + "║" + new string(' ', n) + "║▒";

    internal static string ContentRowShadow(string p, int n, string content) =>
        p + "║" + content.PadRight(n) + "║▒";

    internal static string BottomBorder(string p, int n) =>
        p + "╚" + new string('═', n) + "╝▒";

    internal static string ShadowLine(string p, int n) =>
        p + " " + new string('▒', n + 2);

    // Center 'text' in exactly 'inner' chars (left-biased when odd remainder)
    internal static string Center(string text, int inner) =>
        text.PadLeft((inner + text.Length) / 2).PadRight(inner);
}

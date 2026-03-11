namespace BoardWC.Console.UI;

internal static class MainMenu
{
    internal enum PlayerType { Empty, Human, Ai }

    internal const string SelectTitle = "SELECT PLAYERS";
    internal const string HintText    = "Use \u2191\u2193 and SPACE to select. Press ENTER to continue";

    internal const string OverlayLine1 = "At least two players must be selected to proceed.";
    internal const string OverlayLine2 = "Press ENTER to continue";

    // Box dimensions: inner wide enough for HintText (51 chars) + 4-char margin
    internal const int BoxInner = 56;
    private  const int BoxLines = 13; // top+blank+title+blank+4 players+blank+hint+blank+bottom+shadow

    internal static PlayerType[] Show(IConsoleIO console)
    {
        var types      = new PlayerType[4];
        int cursor     = 0;
        bool overlay   = false;

        while (true)
        {
            Render(console, types, cursor, overlay);
            var key = console.ReadKey(true).Key;

            if (overlay)
            {
                if (key == ConsoleKey.Enter) overlay = false;
                continue;
            }

            switch (key)
            {
                case ConsoleKey.UpArrow:
                    cursor = cursor == 0 ? 3 : cursor - 1;
                    break;
                case ConsoleKey.DownArrow:
                    cursor = (cursor + 1) % 4;
                    break;
                case ConsoleKey.Spacebar:
                    types[cursor] = Advance(types[cursor]);
                    break;
                case ConsoleKey.Enter:
                    if (SelectedCount(types) >= 2)
                        return types;
                    overlay = true;
                    break;
            }
        }
    }

    internal static PlayerType Advance(PlayerType type) => type switch
    {
        PlayerType.Empty => PlayerType.Human,
        PlayerType.Human => PlayerType.Ai,
        _                => PlayerType.Empty,
    };

    internal static int SelectedCount(PlayerType[] types) =>
        types.Count(t => t != PlayerType.Empty);

    internal static void Render(IConsoleIO console, PlayerType[] types, int cursor, bool overlay)
    {
        string p      = new string(' ', ConsoleBox.HorizPad(BoxInner));
        int    topPad = ConsoleBox.VertPad(console.WindowHeight, BoxLines);

        console.SetCursorPosition(0, topPad);
        console.WriteLine(ConsoleBox.TopBorder(p, BoxInner));
        console.WriteLine(ConsoleBox.BlankRow(p, BoxInner));
        console.WriteLine(ConsoleBox.ContentRowShadow(p, BoxInner, ConsoleBox.Center(SelectTitle, BoxInner)));
        console.WriteLine(ConsoleBox.BlankRowShadow(p, BoxInner));

        for (int i = 0; i < 4; i++)
        {
            string marker = types[i] switch
            {
                PlayerType.Human => "H",
                PlayerType.Ai    => "A",
                _                => " ",
            };
            string arrow   = i == cursor ? " <" : "  ";
            string row     = $"PLAYER {i + 1} [{marker}]{arrow}";
            string content = ConsoleBox.Center(row, BoxInner);
            console.WriteColored(ConsoleBox.ContentRowShadow(p, BoxInner, content), PlayerColors.Colors[i]);
        }

        console.WriteLine(ConsoleBox.BlankRowShadow(p, BoxInner));
        console.WriteLine(ConsoleBox.ContentRowShadow(p, BoxInner, ConsoleBox.Center(HintText, BoxInner)));
        console.WriteLine(ConsoleBox.BlankRowShadow(p, BoxInner));
        console.WriteLine(ConsoleBox.BottomBorder(p, BoxInner));
        console.WriteLine(ConsoleBox.ShadowLine(p, BoxInner));

        if (overlay)
            RenderOverlay(console, console.WindowWidth);
    }

    internal static void RenderOverlay(IConsoleIO console, int width)
    {
        int inner    = Math.Max(OverlayLine1.Length, OverlayLine2.Length);
        string top   = "\u2554" + new string('\u2550', inner + 2) + "\u2557";
        string mid1  = "\u2551 " + OverlayLine1.PadRight(inner) + " \u2551";
        string mid2  = "\u2551 " + OverlayLine2.PadRight(inner) + " \u2551";
        string bottom = "\u255A" + new string('\u2550', inner + 2) + "\u255D";

        console.WriteLine("");
        console.WriteLine(Center(top,  width));
        console.WriteLine(Center(mid1, width));
        console.WriteLine(Center(mid2, width));
        console.WriteLine(Center(bottom, width));
    }

    internal static string Center(string text, int width) =>
        new string(' ', Math.Max(0, (width - text.Length) / 2)) + text;
}

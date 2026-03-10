namespace BoardWC.Console.UI;

internal static class MainMenu
{
    internal enum PlayerType { Empty, Human, Ai }

    private static readonly ConsoleColor[] PlayerColors =
        [ConsoleColor.Blue, ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.Yellow];

    internal const string SelectTitle = "SELECT PLAYERS";
    internal const string HintText    = "Use arrow keys UP/DOWN and SPACE to select. Press ENTER to continue";

    internal const string OverlayLine1 = "At least two players must be selected to proceed.";
    internal const string OverlayLine2 = "Press ENTER to continue";

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
        int w = console.WindowWidth;

        console.Clear();
        console.Write(SplashScreen.TitleText);
        console.WriteLine(Center(SelectTitle, w));
        console.WriteLine("");

        for (int i = 0; i < 4; i++)
        {
            string marker = types[i] switch
            {
                PlayerType.Human => "H",
                PlayerType.Ai    => "A",
                _                => " ",
            };
            string arrow = i == cursor ? " <" : "  ";
            string row   = $"PLAYER {i + 1} [{marker}]{arrow}";
            console.WriteColored(Center(row, w), PlayerColors[i]);
        }

        console.WriteLine("");
        console.WriteLine(Center(HintText, w));

        if (overlay)
            RenderOverlay(console, w);
    }

    internal static void RenderOverlay(IConsoleIO console, int width)
    {
        int inner    = Math.Max(OverlayLine1.Length, OverlayLine2.Length);
        string top   = "+" + new string('-', inner + 2) + "+";
        string mid1  = "| " + OverlayLine1.PadRight(inner) + " |";
        string mid2  = "| " + OverlayLine2.PadRight(inner) + " |";

        console.WriteLine("");
        console.WriteLine(Center(top,  width));
        console.WriteLine(Center(mid1, width));
        console.WriteLine(Center(mid2, width));
        console.WriteLine(Center(top,  width));
    }

    internal static string Center(string text, int width) =>
        new string(' ', Math.Max(0, (width - text.Length) / 2)) + text;
}

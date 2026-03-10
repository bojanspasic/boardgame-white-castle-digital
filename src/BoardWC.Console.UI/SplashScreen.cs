namespace BoardWC.Console.UI;

internal interface IConsoleIO
{
    int WindowWidth { get; }
    void Clear();
    void Write(string text);
    void WriteLine(string text);
    ConsoleKeyInfo ReadKey(bool intercept);
}

internal static class SplashScreen
{
    internal static readonly string TitleText = LoadTitleText();

    internal const string Prompt = "-=> PRESS SPACE TO START <=-";

    internal static void Show(IConsoleIO console)
    {
        console.Clear();
        console.Write(TitleText);

        var padding = Math.Max(0, (console.WindowWidth - Prompt.Length) / 2);
        console.WriteLine(new string(' ', padding) + Prompt);

        while (console.ReadKey(true).Key != ConsoleKey.Spacebar)
            ;
    }

    private static string LoadTitleText()
    {
        var assembly = typeof(SplashScreen).Assembly;
        using var stream = assembly.GetManifestResourceStream("BoardWC.Console.UI.title.txt")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

namespace BoardWC.Console.UI;

internal interface IConsoleIO
{
    int WindowWidth  { get; }
    int WindowHeight { get; }
    void Clear();
    void Write(string text);
    void WriteColored(string text, ConsoleColor color);
    void WriteLine(string text);
    ConsoleKeyInfo ReadKey(bool intercept);
}

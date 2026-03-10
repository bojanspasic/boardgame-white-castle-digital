using System.Diagnostics.CodeAnalysis;
using BoardWC.Console.UI;

namespace BoardWC.Console.Presenters;

[ExcludeFromCodeCoverage]
internal sealed class SystemConsoleIO : IConsoleIO
{
    public int WindowWidth => System.Console.WindowWidth;
    public void Clear() => System.Console.Clear();
    public void Write(string text) => System.Console.Write(text);
    public void WriteLine(string text) => System.Console.WriteLine(text);
    public ConsoleKeyInfo ReadKey(bool intercept) => System.Console.ReadKey(intercept);
}

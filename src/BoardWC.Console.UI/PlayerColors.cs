namespace BoardWC.Console.UI;

/// <summary>
/// Slot-based console color palette: Player 1 = Blue, 2 = Red, 3 = Green, 4 = Yellow.
/// Index 0–3 corresponds to player slots as shown in the main menu.
/// </summary>
internal static class PlayerColors
{
    internal static readonly ConsoleColor[] Colors =
        [ConsoleColor.Blue, ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.Yellow];
}

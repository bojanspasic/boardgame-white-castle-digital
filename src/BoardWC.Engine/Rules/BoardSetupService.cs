using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Rules;

/// <summary>
/// Encapsulates the board initialisation steps performed at game start:
/// rolling dice, placing tokens, dealing room cards, setting up training grounds,
/// farming lands, and the top floor card.
/// </summary>
internal static class BoardSetupService
{
    internal static void Setup(Board board, List<Player> players, Random rng)
    {
        board.RollAllDice(players.Count, rng);
        board.PlaceTokens(rng);
        board.PlaceCards(rng);
        board.SetupTrainingGrounds(rng);
        board.SetupFarmingLands(rng);
        board.SetupTopFloorCard(rng);
    }
}

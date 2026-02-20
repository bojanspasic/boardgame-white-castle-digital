using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Engine;

public interface IGameEngine
{
    Guid GameId { get; }
    bool IsGameOver { get; }

    /// <summary>Returns the current state without advancing it.</summary>
    GameStateSnapshot GetCurrentState();

    /// <summary>Submit a player action. Returns new state + events, or a failure.</summary>
    ActionResult ProcessAction(IGameAction action);

    /// <summary>All legal actions for the given player right now.</summary>
    IReadOnlyList<IGameAction> GetLegalActions(Guid playerId);

    /// <summary>Final scores — only populated once IsGameOver is true.</summary>
    IReadOnlyList<PlayerScore>? GetFinalScores();
}

using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;

namespace BoardWC.Engine.AI;

public interface IAiStrategy
{
    string StrategyId { get; }

    /// <summary>
    /// Given a read-only snapshot of the current state and all legal moves,
    /// return the action this AI chooses. Must return one of the provided legalActions.
    /// </summary>
    IGameAction SelectAction(
        GameStateSnapshot state,
        IReadOnlyList<IGameAction> legalActions);
}

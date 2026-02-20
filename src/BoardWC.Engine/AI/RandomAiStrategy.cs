using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;

namespace BoardWC.Engine.AI;

/// <summary>Picks a random legal action. Useful as a baseline opponent and for engine testing.</summary>
public sealed class RandomAiStrategy : IAiStrategy
{
    private readonly Random _rng;

    public string StrategyId => "random";

    public RandomAiStrategy(Random? rng = null) => _rng = rng ?? Random.Shared;

    public IGameAction SelectAction(GameStateSnapshot state, IReadOnlyList<IGameAction> legalActions)
    {
        if (legalActions.Count == 0)
            throw new InvalidOperationException("AI has no legal actions to choose from.");

        return legalActions[_rng.Next(legalActions.Count)];
    }
}

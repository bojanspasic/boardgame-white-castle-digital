using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;

namespace BoardWC.Engine.AI;

/// <summary>
/// Greedy AI: prefers the bridge slot that yields the highest die value.
/// Falls back to pass if nothing better is available.
/// </summary>
public sealed class GreedyResourceAiStrategy : IAiStrategy
{
    public string StrategyId => "greedy-resource";

    public IGameAction SelectAction(GameStateSnapshot state, IReadOnlyList<IGameAction> legalActions)
    {
        if (legalActions.Count == 0)
            throw new InvalidOperationException("AI has no legal actions to choose from.");

        // Score each action — higher is better
        var best = legalActions
            .Select(a => (Action: a, Score: Score(a, state)))
            .OrderByDescending(x => x.Score)
            .First();

        return best.Action;
    }

    private static int Score(IGameAction action, GameStateSnapshot state) => action switch
    {
        TakeDieFromBridgeAction bridge => ScoreBridge(bridge, state),
        PassAction              _      => -1,   // Prefer any action over passing
        _                             => 0,
    };

    private static int ScoreBridge(TakeDieFromBridgeAction a, GameStateSnapshot state)
    {
        var bridge = state.Board.Bridges.FirstOrDefault(b => b.Color == a.BridgeColor);
        if (bridge is null) return 0;
        // Prefer taking higher-value dice
        return a.DiePosition == DiePosition.High
            ? bridge.High?.Value ?? 0
            : bridge.Low?.Value  ?? 0;
    }
}

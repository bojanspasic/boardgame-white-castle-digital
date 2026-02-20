using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;

namespace BoardWC.Engine.AI;

/// <summary>
/// Greedy AI: prefers the bridge slot or tower action that yields the most immediate resources.
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
        TakeDieFromBridgeAction  bridge => ScoreBridge(bridge, state),
        PlaceWorkerInTowerAction tower  => ScoreTower(tower, state),
        PassAction               _      => -1,   // Prefer any action over passing
        _                              => 0,
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

    private static int ScoreTower(PlaceWorkerInTowerAction a, GameStateSnapshot state)
    {
        var tower = state.Board.Towers.FirstOrDefault(t => t.Zone == a.Zone);
        if (tower is null) return 0;
        var level = tower.Levels.FirstOrDefault(l => l.Level == a.Level);
        if (level is null) return 0;

        return level.Action.ActionType switch
        {
            TowerActionType.GainResources  => level.Action.ResourceGain.Total + level.Action.LanternsGained,
            TowerActionType.AdvanceTower   => 2 + level.Action.LanternsGained,
            TowerActionType.AcquireClanCard => 3,
            TowerActionType.GainLanterns   => level.Action.LanternsGained * 2,
            _ => 0
        };
    }
}

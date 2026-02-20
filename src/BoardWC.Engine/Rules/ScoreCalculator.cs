using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Rules;

internal static class ScoreCalculator
{
    public static IReadOnlyList<PlayerScore> Calculate(GameState state) =>
        state.Players
            .Select(p =>
            {
                int lanterns  = p.LanternScore;
                int clanCards = p.ClanCards.Sum(c => c.VictoryPoints);
                int tower     = p.TowerLevels.Values.Sum();
                int total     = lanterns + clanCards + tower;

                return new PlayerScore(p.Id, p.Name, total, lanterns, clanCards, tower);
            })
            .OrderByDescending(s => s.Total)
            .ToList()
            .AsReadOnly();
}

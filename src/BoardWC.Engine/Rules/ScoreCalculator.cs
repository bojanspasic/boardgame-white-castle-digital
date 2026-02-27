using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Rules;

internal static class ScoreCalculator
{
    public static IReadOnlyList<PlayerScore> Calculate(GameState state) =>
        state.Players
            .Select(p =>
            {
                int lanterns = p.LanternScore;

                return new PlayerScore(p.Id, p.Name, lanterns, lanterns);
            })
            .OrderByDescending(s => s.Total)
            .ToList()
            .AsReadOnly();
}

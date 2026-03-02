using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Rules;

internal static class ScoreCalculator
{
    public static IReadOnlyList<PlayerScore> Calculate(GameState state) =>
        state.Players
            .Select(p => CalcPlayer(p, state))
            .OrderByDescending(s => s.Total)
            .ToList()
            .AsReadOnly();

    private static PlayerScore CalcPlayer(Player p, GameState state)
    {
        int lanterns  = p.LanternScore;
        int courtiers = p.CourtiersAtGate * 1
                      + p.CourtiersOnGroundFloor * 3
                      + p.CourtiersOnMidFloor    * 6
                      + p.CourtiersOnTopFloor     * 10;
        int coins     = p.Coins / 5;
        int seals     = p.MonarchialSeals / 5;
        int resources = ResourceVP(p.Resources.Food)
                      + ResourceVP(p.Resources.Iron)
                      + ResourceVP(p.Resources.ValueItem);
        int farm      = FarmVP(p, state);

        int total = lanterns + courtiers + coins + seals + resources + farm;
        return new PlayerScore(p.Id, p.Name, total,
            lanterns, courtiers, coins, seals, resources, farm);
    }

    /// <summary>4+ of one resource = 1 VP; 7 = 2 VP.</summary>
    private static int ResourceVP(int amount) =>
        amount >= 7 ? 2 : amount >= 4 ? 1 : 0;

    /// <summary>Sum VPs from every farm field where this player has a farmer.</summary>
    private static int FarmVP(Player p, GameState state)
    {
        int vp = 0;
        foreach (var (_, _, field) in state.Board.AllFarmFields())
            if (field.HasFarmer(p.Name))
                vp += field.Card.VictoryPoints;
        return vp;
    }
}

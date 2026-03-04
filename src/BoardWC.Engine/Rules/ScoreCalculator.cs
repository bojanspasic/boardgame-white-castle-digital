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
                      + p.CourtiersOnStewardFloor * 3
                      + p.CourtiersOnDiplomatFloor    * 6
                      + p.CourtiersOnTopFloor     * 10;
        int coins     = p.Coins / 5;
        int seals     = p.DaimyoSeals / 5;
        int resources = ResourceVP(p.Resources.Food)
                      + ResourceVP(p.Resources.Iron)
                      + ResourceVP(p.Resources.MotherOfPearls);
        int farm      = FarmVP(p, state);
        int tg        = TrainingGroundsVP(p, state);
        int influence = InfluenceVP(p.Influence);

        int total = lanterns + courtiers + coins + seals + resources + farm + tg + influence;
        return new PlayerScore(p.Id, p.Name, total,
            lanterns, courtiers, coins, seals, resources, farm, tg, influence);
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

    /// <summary>
    /// Training grounds VP:
    /// - Areas 0+1 (iron cost 1 and 3): soldiers × castle courtiers
    /// - Area  2   (iron cost 5):        soldiers × 2 × castle courtiers
    /// Castle courtiers = steward + diplomat + top (gate excluded).
    /// </summary>
    private static int TrainingGroundsVP(Player p, GameState state)
    {
        int castleCourtiers = p.CourtiersOnStewardFloor
                            + p.CourtiersOnDiplomatFloor
                            + p.CourtiersOnTopFloor;
        if (castleCourtiers == 0) return 0;

        var areas = state.Board.TrainingGrounds.Areas;

        int soldiers12 = CountSoldiers(areas[0], p.Name)
                       + CountSoldiers(areas[1], p.Name);
        int soldiers3  = CountSoldiers(areas[2], p.Name);

        return (soldiers12 * castleCourtiers) + (soldiers3 * 2 * castleCourtiers);
    }

    private static int CountSoldiers(TrainingGroundsArea area, string playerName)
    {
        int count = 0;
        foreach (var name in area.SoldierOwners)
            if (name == playerName) count++;
        return count;
    }

    /// <summary>
    /// Influence VP table:
    /// 0–5 = 0, 6–10 = 3, 11–15 = 6, 16 = 7, 17 = 8, 18 = 9, 19 = 10, 20+ = 11.
    /// </summary>
    private static int InfluenceVP(int influence) => influence switch
    {
        <= 5  => 0,
        <= 10 => 3,
        <= 15 => 6,
        16    => 7,
        17    => 8,
        18    => 9,
        19    => 10,
        _     => 11,
    };
}

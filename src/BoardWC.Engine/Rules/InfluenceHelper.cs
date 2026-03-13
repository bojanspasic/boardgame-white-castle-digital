using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal static class InfluenceHelper
{
    /// <summary>
    /// Applies an influence gain. If the gain would cross a threshold (5, 10, or 15),
    /// sets <see cref="PlayerPendingState.InfluenceGain"/> and
    /// <see cref="PlayerPendingState.InfluenceSealCost"/> and emits
    /// <see cref="InfluenceGainPendingEvent"/> instead of applying directly.
    /// Returns true when a pending state was set (caller must hold the turn).
    /// </summary>
    public static bool Apply(Player player, int influenceGain, GameState state, List<IDomainEvent> events)
    {
        if (influenceGain <= 0) return false;

        int current  = player.Influence;
        int newTotal = current + influenceGain;
        int cost     = SealCost(current, newTotal);

        if (cost == 0)
        {
            player.Influence        += influenceGain;
            player.InfluenceGainOrder = ++state.InfluenceGainCounter;
            return false;
        }

        // Threshold crossed — player must decide whether to pay
        player.Pending.InfluenceGain     = influenceGain;
        player.Pending.InfluenceSealCost = cost;
        events.Add(new InfluenceGainPendingEvent(state.GameId, player.Id, influenceGain, cost));
        return true;
    }

    /// <summary>
    /// Returns the total Daimyo Seal cost to move from <paramref name="current"/>
    /// influence to <paramref name="newTotal"/>, crossing any of the thresholds 5, 10, 15.
    /// </summary>
    public static int SealCost(int current, int newTotal)
    {
        int cost = 0;
        if (current < 5  && newTotal >= 5)  cost += 1;
        if (current < 10 && newTotal >= 10) cost += 2;
        if (current < 15 && newTotal >= 15) cost += 3;
        return cost;
    }
}

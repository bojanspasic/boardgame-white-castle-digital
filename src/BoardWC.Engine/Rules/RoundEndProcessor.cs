using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

/// <summary>
/// Executes all round-end effects: emits the round-ended event, fires farm effects,
/// clears placement areas, advances the round counter, selects the next first player,
/// re-rolls dice, and refreshes training grounds tokens.
/// On the final round, transitions the game to <see cref="Phase.GameOver"/> instead.
/// </summary>
internal static class RoundEndProcessor
{
    internal static void Execute(GameState state, List<IDomainEvent> events)
    {
        events.Add(new RoundEndedEvent(state.GameId, state.CurrentRound));

        // Fire farm effects for bridges that still have dice (rounds 1 and 2 only)
        if (state.CurrentRound < state.MaxRounds)
            FireRoundEndFarmEffects(state, events);

        state.Board.ClearPlacementAreas();

        // Clear personal domain placed dice so rows are available again next round
        foreach (var p in state.Players)
            foreach (var row in p.PersonalDomainRows)
                row.ClearForRound();

        if (state.CurrentRound >= state.MaxRounds)
        {
            state.CurrentPhase = Phase.GameOver;
            var scores = ScoreCalculator.Calculate(state);
            events.Add(new GameOverEvent(state.GameId, scores));
        }
        else
        {
            state.CurrentRound++;
            state.ActivePlayerIndex = FirstPlayerByInfluence(state.Players);
            // Roll fresh dice and re-draw training grounds tokens for the new round
            state.Board.RollAllDice(state.Players.Count, state.Rng);
            state.Board.SetupTrainingGrounds(state.Rng);
        }
    }

    /// <summary>
    /// Returns the index of the player who goes first in the next round.
    /// Rule: player with the highest Influence score goes first.
    /// Tiebreaker: among equally highest-influence players, the one who gained influence
    /// most recently (<see cref="Player.InfluenceGainOrder"/> is highest) goes first.
    /// Fallback: player 0 (preserves original order when no influence has been gained).
    /// </summary>
    private static int FirstPlayerByInfluence(List<Player> players)
    {
        int maxInfluence = 0;
        for (int i = 0; i < players.Count; i++)
            if (players[i].Influence > maxInfluence)
                maxInfluence = players[i].Influence;

        int bestIndex = 0;
        int bestOrder = -1;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p.Influence == maxInfluence && p.InfluenceGainOrder > bestOrder)
            {
                bestOrder = p.InfluenceGainOrder;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private static void FireRoundEndFarmEffects(GameState state, List<IDomainEvent> events)
    {
        foreach (var bridge in state.Board.Bridges)
        {
            if (bridge.DiceCount <= 0) continue;

            foreach (var (color, isInland, field) in state.Board.AllFarmFields()
                         .Where(f => f.Color == bridge.Color))
            {
                foreach (var farmerName in field.FarmerOwners)
                {
                    var owner = state.Players.FirstOrDefault(p => p.Name == farmerName);
                    if (owner is null) continue;

                    var (resources, coins, seals, lantern, action) =
                        FarmHandler.ApplyCardEffect(field.Card, owner);

                    LanternHelper.Apply(owner, lantern, state.GameId, events);

                    events.Add(new FarmEffectFiredEvent(
                        state.GameId, owner.Id,
                        color, isInland,
                        resources, coins, seals, lantern, action));
                }
            }
        }
    }
}

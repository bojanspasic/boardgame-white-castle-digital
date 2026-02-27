using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal static class PostActionProcessor
{
    public static void Run(GameState state, List<IDomainEvent> events)
    {
        if (state.CurrentPhase != Phase.WorkerPlacement) return;

        // Active player has taken a die but hasn't placed it yet — hold the turn
        if (state.ActivePlayer.DiceInHand.Count > 0) return;

        // Active player has pending AnyResource choices to resolve — hold the turn
        if (state.ActivePlayer.PendingAnyResourceChoices > 0) return;

        // Active player has pending training grounds actions to resolve — hold the turn
        if (state.ActivePlayer.PendingTrainingGroundsActions > 0) return;

        // Active player has pending castle actions to resolve — hold the turn
        if (state.ActivePlayer.CastlePlaceRemaining > 0 ||
            state.ActivePlayer.CastleAdvanceRemaining > 0) return;

        // Round ends when 3 or fewer dice remain across all bridges
        if (state.Board.TotalDiceRemaining <= 3)
            EndRound(state, events);
        else
            state.AdvanceTurn();
    }

    private static void EndRound(GameState state, List<IDomainEvent> events)
    {
        events.Add(new RoundEndedEvent(state.GameId, state.CurrentRound));

        state.Board.ClearPlacementAreas();

        if (state.CurrentRound >= state.MaxRounds)
        {
            state.CurrentPhase = Phase.GameOver;
            var scores = ScoreCalculator.Calculate(state);
            events.Add(new GameOverEvent(state.GameId, scores));
        }
        else
        {
            state.CurrentRound++;
            state.ActivePlayerIndex = 0;
            // Roll fresh dice and re-draw training grounds tokens for the new round
            state.Board.RollAllDice(state.Players.Count, state.Rng);
            state.Board.SetupTrainingGrounds(state.Rng);
        }
    }
}

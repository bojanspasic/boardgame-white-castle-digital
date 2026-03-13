using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal static class PostActionProcessor
{
    public static void Run(GameState state, List<IDomainEvent> events)
    {
        // Seed card selection phase: hold for AnyResource, then advance players or enter gameplay
        if (state.CurrentPhase == Phase.SeedCardSelection)
        {
            if (state.ActivePlayer.Pending.AnyResourceChoices > 0) return;
            if (state.Players.All(p => p.SeedCard is not null))
            {
                state.ActivePlayerIndex = 0;
                state.CurrentPhase = Phase.WorkerPlacement;
                return;
            }
            state.AdvanceTurn();
            return;
        }

        if (state.CurrentPhase != Phase.WorkerPlacement) return;

        // Active player has taken a die but hasn't placed it yet — hold the turn
        if (state.ActivePlayer.DiceInHand.Count > 0) return;

        // Hold turn while any pending state exists
        if (TurnAdvancePolicy.HasPendingState(state.ActivePlayer)) return;

        // Round ends when 3 or fewer dice remain across all bridges
        if (state.Board.TotalDiceRemaining <= 3)
            RoundEndProcessor.Execute(state, events);
        else
            state.AdvanceTurn();
    }
}

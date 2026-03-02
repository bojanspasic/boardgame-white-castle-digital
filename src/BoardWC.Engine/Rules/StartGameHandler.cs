using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class StartGameHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is StartGameAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        if (state.CurrentPhase != Phase.Setup)
            return ValidationResult.Fail("Game has already started.");
        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        state.Board.RollAllDice(state.Players.Count, state.Rng);
        state.Board.PlaceTokens(state.Rng);
        state.Board.PlaceCards(state.Rng);
        state.Board.SetupTrainingGrounds(state.Rng);
        state.Board.SetupFarmingLands(state.Rng);
        state.Board.SetupTopFloorCard(state.Rng);

        var pairs = SeedCardDecks.DrawPairs(state.Players.Count + 1, state.Rng);
        state.SeedCardPairs.AddRange(pairs);

        state.CurrentPhase = Phase.SeedCardSelection;
        events.Add(new GameStartedEvent(state.GameId));
    }
}

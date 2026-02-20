using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class PassHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is PassAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a = (PassAction)action;

        if (state.CurrentPhase != Phase.WorkerPlacement)
            return ValidationResult.Fail("Not in worker placement phase.");

        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");
        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (PassAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);

        events.Add(new PlayerPassedEvent(state.GameId, player.Id));
    }
}

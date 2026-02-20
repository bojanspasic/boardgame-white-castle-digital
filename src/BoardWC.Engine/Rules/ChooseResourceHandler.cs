using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class ChooseResourceHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is ChooseResourceAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a      = (ChooseResourceAction)action;
        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (player.PendingAnyResourceChoices <= 0)
            return ValidationResult.Fail("No pending resource choice.");
        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (ChooseResourceAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);
        player.PendingAnyResourceChoices--;
        player.Resources = player.Resources.Add(a.Choice, 1).Clamp(7);
        events.Add(new AnyResourceChosenEvent(state.GameId, player.Id, a.Choice));
    }
}
